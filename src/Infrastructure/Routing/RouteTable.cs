using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shop.Application.Routing;
using Shop.Domain.Routing;
using Shop.Infrastructure.Persistence;

namespace Shop.Infrastructure.Routing;

/// <summary>
/// The whole route table in memory, so resolving a request path takes no SQL (docs/11 C.5). Two dictionaries inside an immutable
/// snapshot that is swapped atomically: <c>path → row</c> and <c>(type, id) → current path</c>. It is built at startup, rebuilt
/// right after every write on this instance, and a background check every <see cref="Interval"/> rebuilds it when another
/// instance changed the table (row count, newest change or highest id differs).
/// </summary>
public sealed class RouteTable(IServiceScopeFactory scopes, ILogger<RouteTable> logger) : IRouteTable
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private sealed record Row(string Type, int TargetId, bool IsCurrent, bool IsGone);

    private sealed record Snapshot(
        Dictionary<string, Row> ByPath,
        Dictionary<(string Type, int Id), string> CurrentPath,
        Dictionary<(string Type, int Id), string> Slugs,
        int Count,
        int MaxId,
        DateTime Newest);

    private volatile Snapshot _snapshot = new(
        new Dictionary<string, Row>(StringComparer.OrdinalIgnoreCase), [], [], 0, 0, DateTime.MinValue);

    public RouteResolutionDto Resolve(string? requestPath)
    {
        var snapshot = _snapshot;
        var normalized = RoutePath.Normalize(requestPath);
        if (!RoutePath.IsWellFormed(normalized) || !snapshot.ByPath.TryGetValue(normalized, out var row))
        {
            return RouteResolutionDto.Missing;
        }

        if (row.IsGone)
        {
            return new RouteResolutionDto(RouteResolutionDto.Gone);
        }

        var key = (row.Type, row.TargetId);
        if (!snapshot.Slugs.TryGetValue(key, out var slug) || !snapshot.CurrentPath.TryGetValue(key, out var current))
        {
            return RouteResolutionDto.Missing; // a row whose entity is gone without a tombstone: nothing to show
        }

        // An old path, or the current one spelled differently (upper case, trailing slash): always ONE hop to the canonical path.
        if (!row.IsCurrent || !string.Equals(requestPath, current, StringComparison.Ordinal))
        {
            return new RouteResolutionDto(RouteResolutionDto.Redirect, To: current, Status: 308);
        }

        return new RouteResolutionDto(row.Type, row.TargetId, slug, current);
    }

    public string PathOf(string targetType, int targetId, string slug) =>
        _snapshot.CurrentPath.TryGetValue((targetType, targetId), out var path) ? path : RouteTargetTypes.DefaultPath(targetType, slug);

    public string? CurrentPathOf(string targetType, int targetId)
    {
        var snapshot = _snapshot;
        var key = (targetType, targetId);
        return snapshot.Slugs.ContainsKey(key) && snapshot.CurrentPath.TryGetValue(key, out var path) ? path : null;
    }

    private int _version;

    public int Version => Volatile.Read(ref _version);

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
        var rows = await db.RoutePaths.AsNoTracking()
            .Select(r => new { r.Id, r.Path, r.TargetType, r.TargetId, r.IsCurrent, r.IsGone, r.UpdatedAtUtc })
            .ToListAsync(ct);

        var byPath = new Dictionary<string, Row>(StringComparer.OrdinalIgnoreCase);
        var current = new Dictionary<(string, int), string>();
        foreach (var r in rows)
        {
            byPath[r.Path] = new Row(r.TargetType, r.TargetId, r.IsCurrent, r.IsGone);
            if (r.IsCurrent)
            {
                current[(r.TargetType, r.TargetId)] = r.Path;
            }
        }

        var slugs = new Dictionary<(string, int), string>();
        foreach (var c in await db.Categories.AsNoTracking().Select(x => new { x.Id, x.Slug }).ToListAsync(ct))
        {
            slugs[(RouteTargetTypes.Category, c.Id)] = c.Slug;
        }

        foreach (var p in await db.Products.AsNoTracking().Select(x => new { x.Id, x.Slug }).ToListAsync(ct))
        {
            slugs[(RouteTargetTypes.Product, p.Id)] = p.Slug;
        }

        foreach (var p in await db.ContentPages.AsNoTracking().Select(x => new { x.Id, x.Slug }).ToListAsync(ct))
        {
            slugs[(RouteTargetTypes.ContentPage, p.Id)] = p.Slug;
        }

        foreach (var p in await db.BlogPosts.AsNoTracking().Select(x => new { x.Id, x.Slug }).ToListAsync(ct))
        {
            slugs[(RouteTargetTypes.BlogPost, p.Id)] = p.Slug;
        }

        _snapshot = new Snapshot(
            byPath, current, slugs, rows.Count, rows.Count == 0 ? 0 : rows.Max(r => r.Id), rows.Count == 0 ? DateTime.MinValue : rows.Max(r => r.UpdatedAtUtc));
        Interlocked.Increment(ref _version);
    }

    /// <summary>Rebuilds only when the table changed since the last build (one aggregate query).</summary>
    public async Task RefreshIfChangedAsync(CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
        var state = await db.RoutePaths.AsNoTracking().GroupBy(_ => 1)
            .Select(g => new { Count = g.Count(), MaxId = g.Max(r => r.Id), Newest = g.Max(r => r.UpdatedAtUtc) }).FirstOrDefaultAsync(ct);
        var snapshot = _snapshot;
        if ((state?.Count ?? 0) != snapshot.Count || (state?.MaxId ?? 0) != snapshot.MaxId || (state?.Newest ?? DateTime.MinValue) != snapshot.Newest)
        {
            await ReloadAsync(ct);
            logger.LogInformation("Route table reloaded after a change from another instance");
        }
    }
}

/// <summary>Keeps every instance's table fresh without a per-request query.</summary>
public sealed class RouteRefreshService(RouteTable table, ILogger<RouteRefreshService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RouteTable.Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await table.RefreshIfChangedAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Route table refresh failed; the previous table stays in use");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }
}
