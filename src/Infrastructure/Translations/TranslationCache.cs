using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shop.Application.Translations;
using Shop.Infrastructure.Persistence;

namespace Shop.Infrastructure.Translations;

/// <summary>
/// In-memory snapshot of the edited texts: an immutable dictionary swapped atomically, so lookups take no lock. It is loaded at
/// startup, rebuilt right after an admin edit on this instance (<see cref="ReloadAsync"/>), and a background check every
/// <see cref="Interval"/> reloads when another instance changed the table (row count or newest change differs).
/// Only rows that differ from the shipped default are kept: for everything else the shipped text is already the answer.
/// </summary>
public sealed class TranslationCache(IServiceScopeFactory scopes, ILogger<TranslationCache> logger) : ITranslationSource
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private sealed record Snapshot(IReadOnlyDictionary<(string Culture, string Key), string> Edited, int Count, DateTime Newest);

    private volatile Snapshot _snapshot = new(new Dictionary<(string, string), string>(), 0, DateTime.MinValue);

    public string? Lookup(string key, string culture) => _snapshot.Edited.TryGetValue((culture, key), out var text) ? text : null;

    public async Task ReloadAsync(CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
        var rows = await db.Translations.AsNoTracking().Select(t => new { t.Title, t.Culture, t.Text, t.DefaultText, t.UpdatedAtUtc }).ToListAsync(ct);
        var edited = rows.Where(r => !string.Equals(r.Text, r.DefaultText, StringComparison.Ordinal))
            .ToDictionary(r => (r.Culture, r.Title), r => r.Text);
        _snapshot = new Snapshot(edited, rows.Count, rows.Count == 0 ? DateTime.MinValue : rows.Max(r => r.UpdatedAtUtc));
    }

    /// <summary>Reloads only when the table changed since the last load (cheap: one aggregate query).</summary>
    public async Task RefreshIfChangedAsync(CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
        var state = await db.Translations.AsNoTracking().GroupBy(_ => 1).Select(g => new { Count = g.Count(), Newest = g.Max(t => t.UpdatedAtUtc) }).FirstOrDefaultAsync(ct);
        var count = state?.Count ?? 0;
        var newest = state?.Newest ?? DateTime.MinValue;
        if (count != _snapshot.Count || newest != _snapshot.Newest)
        {
            await ReloadAsync(ct);
            logger.LogInformation("Translations reloaded after a change from another instance");
        }
    }
}

/// <summary>Connects the cache to <see cref="Translate"/> and keeps it fresh across instances.</summary>
public sealed class TranslationRefreshService(TranslationCache cache, ILoggerFactory loggers) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Translate.Configure(cache, loggers.CreateLogger("Shop.Translations"));
        using var timer = new PeriodicTimer(TranslationCache.Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await cache.RefreshIfChangedAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    loggers.CreateLogger("Shop.Translations").LogWarning(ex, "Translation refresh failed; the previous texts stay in use");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }
}
