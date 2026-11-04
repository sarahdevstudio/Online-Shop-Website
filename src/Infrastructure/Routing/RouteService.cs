using Microsoft.EntityFrameworkCore;
using Shop.Application.Catalog;
using Shop.Application.Common;
using Shop.Application.Routing;
using Shop.Domain.Common;
using Shop.Domain.Routing;
using Shop.Infrastructure.Persistence;

namespace Shop.Infrastructure.Routing;

/// <summary>
/// The write side of the route table (docs/11 C.2). Changing an entity's path is one transaction: the old current row becomes a
/// non-current row of the SAME entity (so an old URL is always one hop from the current one), the new or re-taken row becomes
/// current, an audit row is written, and the in-memory table is rebuilt. Nothing is deleted automatically.
/// </summary>
public sealed class RouteService(ShopDbContext db, ICurrentUser currentUser, RouteTable table, TimeProvider clock) : IRouteService
{
    private const int PageSize = 30;

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    private static Error TargetNotFound() => Error.NotFound("target_not_found", "api.error.route-target-not-found");

    private static Error Stale() => Error.Conflict("concurrent_update", "api.error.route-concurrent-update");

    private static Error FieldError(string messageKey) => Error.Validation(new Dictionary<string, string[]> { ["path"] = [messageKey] });

    // ---- reads --------------------------------------------------------------------------------------------------------

    public async Task<Result<RouteTargetDto>> GetAsync(string type, int id, CancellationToken ct)
    {
        if (!RouteTargetTypes.IsKnown(type) || await SlugOfAsync(type, id, ct) is not { } slug)
        {
            return TargetNotFound();
        }

        var current = await EnsureCurrentAsync(type, id, slug, ct);
        return current.IsSuccess ? await LoadTargetAsync(type, id, ct) : current.Error!;
    }

    public async Task<PagedList<RedirectDto>> ListRedirectsAsync(string? q, int page, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        var term = (q ?? string.Empty).Trim().ToLowerInvariant();
        var persian = PersianText.NormalizeLetters(q);

        // Names live in four tables, so the matching is done over the (small) set of non-current rows in memory.
        var rows = await db.RoutePaths.AsNoTracking().Where(r => !r.IsCurrent).OrderByDescending(r => r.UpdatedAtUtc).ThenByDescending(r => r.Id).ToListAsync(ct);
        var names = await NamesAsync(rows.Select(r => (r.TargetType, r.TargetId)).Distinct().ToList(), ct);
        var currents = await db.RoutePaths.AsNoTracking().Where(r => r.IsCurrent).Select(r => new { r.TargetType, r.TargetId, r.Path }).ToListAsync(ct);
        var currentByTarget = currents.ToDictionary(c => (c.TargetType, c.TargetId), c => c.Path);

        var items = rows
            .Select(r => new RedirectDto(
                r.Id, r.Path, r.TargetType, r.TargetId,
                names.GetValueOrDefault((r.TargetType, r.TargetId)) ?? string.Empty,
                currentByTarget.GetValueOrDefault((r.TargetType, r.TargetId)),
                r.IsGone, r.CreatedAtUtc))
            .Where(r => term.Length == 0
                || r.Path.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (r.CurrentPath?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || r.TargetName.Contains(persian, StringComparison.Ordinal))
            .ToList();
        return new PagedList<RedirectDto>(items.Skip((page - 1) * PageSize).Take(PageSize).ToList(), page, PageSize, items.Count);
    }

    public async Task<PagedList<RouteAuditEntryDto>> AuditAsync(int page, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        var total = await db.RouteAuditLogs.CountAsync(ct);
        var rows = await (
                from a in db.RouteAuditLogs.AsNoTracking()
                join u in db.Users.AsNoTracking() on a.AdminUserId equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby a.AtUtc descending, a.Id descending
                select new { Log = a, Username = u.UserName })
            .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct);
        var names = await NamesAsync(rows.Select(r => (r.Log.TargetType, r.Log.TargetId)).Distinct().ToList(), ct);
        var items = rows.Select(r => new RouteAuditEntryDto(
            r.Log.AtUtc, r.Log.TargetType, r.Log.TargetId, names.GetValueOrDefault((r.Log.TargetType, r.Log.TargetId)) ?? string.Empty,
            r.Log.Action, r.Log.OldPath, r.Log.NewPath, r.Username)).ToList();
        return new PagedList<RouteAuditEntryDto>(items, page, PageSize, total);
    }

    // ---- change a path -------------------------------------------------------------------------------------------------

    public async Task<Result<RouteTargetDto>> ChangeAsync(string type, int id, ChangeRoutePathRequest request, CancellationToken ct)
    {
        if (!RouteTargetTypes.IsKnown(type) || await SlugOfAsync(type, id, ct) is not { } slug)
        {
            return TargetNotFound();
        }

        var path = RoutePath.Normalize(request.Path);
        if (RoutePath.Validate(path) is { } invalid)
        {
            return FieldError(invalid);
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var ensured = await EnsureCurrentAsync(type, id, slug, ct);
        if (!ensured.IsSuccess)
        {
            return ensured.Error!;
        }

        var current = await db.RoutePaths.SingleAsync(r => r.TargetType == type && r.TargetId == id && r.IsCurrent, ct);
        if (request.RowVersion is not null && !current.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Stale();
        }

        if (string.Equals(current.Path, path, StringComparison.OrdinalIgnoreCase))
        {
            return await LoadTargetAsync(type, id, ct); // nothing to change
        }

        var existing = await db.RoutePaths.SingleOrDefaultAsync(r => r.Path == path, ct);
        if (existing is not null && !(existing.TargetType == type && existing.TargetId == id && !existing.IsGone))
        {
            return await TakenAsync(existing, ct);
        }

        if (existing is null)
        {
            var olds = await db.RoutePaths.CountAsync(r => r.TargetType == type && r.TargetId == id && !r.IsCurrent && !r.IsGone, ct);
            if (olds + 1 > RouteEntry.MaxOldPathsPerTarget)
            {
                return Error.Conflict("path_limit", Msg.Of("api.error.route-limit", ("max", RouteEntry.MaxOldPathsPerTarget)));
            }
        }

        var now = Now;
        var oldPath = current.Path;
        try
        {
            // Two steps, because the filtered unique index allows one current row per entity at any moment.
            current.SetCurrent(false, now);
            await db.SaveChangesAsync(ct);

            if (existing is null)
            {
                db.RoutePaths.Add(RouteEntry.Create(path, type, id, true, currentUser.UserId, now));
            }
            else
            {
                existing.SetCurrent(true, now); // taking back one of its own former paths
            }

            db.RouteAuditLogs.Add(RouteAuditLog.Create(type, id, RouteAuditLog.Changed, oldPath, path, currentUser.UserId, now));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Stale();
        }
        catch (DbUpdateException)
        {
            // Someone took the path between the check and the write: the unique index decides.
            return Error.Conflict("path_taken", Msg.Of("api.error.route-taken", ("owner", await NameOfAsync(type, id, ct) ?? string.Empty)));
        }

        await table.ReloadAsync(ct);
        return await LoadTargetAsync(type, id, ct);
    }

    // ---- releasing an old path ----------------------------------------------------------------------------------------

    public async Task<Result> ReleaseAsync(int routeId, CancellationToken ct)
    {
        var row = await db.RoutePaths.SingleOrDefaultAsync(r => r.Id == routeId, ct);
        if (row is null)
        {
            return Result.Failure(Error.NotFound("route_not_found", "api.error.route-not-found"));
        }

        if (row.IsCurrent)
        {
            return Result.Failure(Error.Conflict("route_is_current", "api.error.route-is-current"));
        }

        db.RoutePaths.Remove(row);
        db.RouteAuditLogs.Add(RouteAuditLog.Create(row.TargetType, row.TargetId, RouteAuditLog.Released, row.Path, null, currentUser.UserId, Now));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(Stale());
        }

        await table.ReloadAsync(ct);
        return Result.Success();
    }

    // ---- hooks used by the entity services ----------------------------------------------------------------------------

    public async Task<Result> CheckAvailableForNewAsync(string type, string slug, string? path, CancellationToken ct)
    {
        var wanted = string.IsNullOrWhiteSpace(path) ? RouteTargetTypes.DefaultPath(type, slug) : RoutePath.Normalize(path);
        if (RoutePath.Validate(wanted) is { } invalid)
        {
            return Result.Failure(FieldError(invalid));
        }

        var existing = await db.RoutePaths.AsNoTracking().SingleOrDefaultAsync(r => r.Path == wanted, ct);
        return existing is null ? Result.Success() : Result.Failure(await TakenAsync(existing, ct));
    }

    public async Task<Result> AddForNewAsync(string type, int id, string slug, string? path, CancellationToken ct)
    {
        var wanted = string.IsNullOrWhiteSpace(path) ? RouteTargetTypes.DefaultPath(type, slug) : RoutePath.Normalize(path);
        var now = Now;
        db.RoutePaths.Add(RouteEntry.Create(wanted, type, id, true, currentUser.UserId, now));
        db.RouteAuditLogs.Add(RouteAuditLog.Create(type, id, RouteAuditLog.Created, null, wanted, currentUser.UserId, now));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Result.Failure(Error.Conflict("path_taken", Msg.Of("api.error.route-taken", ("owner", await NameOfAsync(type, id, ct) ?? string.Empty))));
        }

        return Result.Success();
    }

    public async Task MarkGoneAsync(string type, int id, CancellationToken ct)
    {
        var rows = await db.RoutePaths.Where(r => r.TargetType == type && r.TargetId == id && !r.IsGone).ToListAsync(ct);
        var now = Now;
        var current = rows.FirstOrDefault(r => r.IsCurrent)?.Path;
        foreach (var row in rows)
        {
            row.MarkGone(now);
        }

        if (rows.Count > 0)
        {
            db.RouteAuditLogs.Add(RouteAuditLog.Create(type, id, RouteAuditLog.DeletedWithTarget, current, null, currentUser.UserId, now));
            await db.SaveChangesAsync(ct);
        }
    }

    public Task CompleteAsync(CancellationToken ct) => table.ReloadAsync(ct);

    // ---- helpers ------------------------------------------------------------------------------------------------------

    /// <summary>An entity that has no row yet (created before the feature, or by a path that skips the service) gets its default path.</summary>
    private async Task<Result> EnsureCurrentAsync(string type, int id, string slug, CancellationToken ct)
    {
        if (await db.RoutePaths.AnyAsync(r => r.TargetType == type && r.TargetId == id && (r.IsCurrent || r.IsGone), ct))
        {
            return Result.Success();
        }

        var added = await AddForNewAsync(type, id, slug, null, ct);
        if (added.IsSuccess)
        {
            await table.ReloadAsync(ct);
        }
        else
        {
            db.ChangeTracker.Clear();
        }

        return added;
    }

    private async Task<Result<RouteTargetDto>> LoadTargetAsync(string type, int id, CancellationToken ct)
    {
        var rows = await db.RoutePaths.AsNoTracking().Where(r => r.TargetType == type && r.TargetId == id && !r.IsGone)
            .OrderByDescending(r => r.UpdatedAtUtc).ThenByDescending(r => r.Id).ToListAsync(ct);
        var current = rows.FirstOrDefault(r => r.IsCurrent);
        return current is null
            ? TargetNotFound()
            : new RouteTargetDto(type, id, current.Path, current.RowVersion,
                rows.Where(r => !r.IsCurrent).Select(r => new OldRoutePathDto(r.Id, r.Path, r.CreatedAtUtc)).ToList());
    }

    /// <summary>The clear Persian conflict: who owns the path, and whether it is a live page or an old redirect.</summary>
    private async Task<Error> TakenAsync(RouteEntry owner, CancellationToken ct)
    {
        if (owner.IsGone)
        {
            return Error.Conflict("path_taken_gone", "api.error.route-taken-gone");
        }

        var name = await NameOfAsync(owner.TargetType, owner.TargetId, ct) ?? string.Empty;
        return owner.IsCurrent
            ? Error.Conflict("path_taken", Msg.Of("api.error.route-taken", ("owner", name)))
            : Error.Conflict("path_taken_redirect", Msg.Of("api.error.route-taken-redirect", ("owner", name)));
    }

    private Task<string?> SlugOfAsync(string type, int id, CancellationToken ct) => type switch
    {
        RouteTargetTypes.Category => db.Categories.AsNoTracking().Where(x => x.Id == id).Select(x => (string?)x.Slug).SingleOrDefaultAsync(ct),
        RouteTargetTypes.Product => db.Products.AsNoTracking().Where(x => x.Id == id).Select(x => (string?)x.Slug).SingleOrDefaultAsync(ct),
        RouteTargetTypes.ContentPage => db.ContentPages.AsNoTracking().Where(x => x.Id == id).Select(x => (string?)x.Slug).SingleOrDefaultAsync(ct),
        RouteTargetTypes.BlogPost => db.BlogPosts.AsNoTracking().Where(x => x.Id == id).Select(x => (string?)x.Slug).SingleOrDefaultAsync(ct),
        _ => Task.FromResult<string?>(null),
    };

    private Task<string?> NameOfAsync(string type, int id, CancellationToken ct) => type switch
    {
        RouteTargetTypes.Category => db.Categories.AsNoTracking().Where(x => x.Id == id).Select(x => (string?)x.Name).SingleOrDefaultAsync(ct),
        RouteTargetTypes.Product => db.Products.AsNoTracking().Where(x => x.Id == id).Select(x => (string?)x.Name).SingleOrDefaultAsync(ct),
        RouteTargetTypes.ContentPage => db.ContentPages.AsNoTracking().Where(x => x.Id == id).Select(x => (string?)x.Title).SingleOrDefaultAsync(ct),
        RouteTargetTypes.BlogPost => db.BlogPosts.AsNoTracking().Where(x => x.Id == id).Select(x => (string?)x.Title).SingleOrDefaultAsync(ct),
        _ => Task.FromResult<string?>(null),
    };

    private async Task<Dictionary<(string Type, int Id), string>> NamesAsync(IReadOnlyCollection<(string Type, int Id)> targets, CancellationToken ct)
    {
        var result = new Dictionary<(string, int), string>();
        foreach (var group in targets.GroupBy(t => t.Type))
        {
            var ids = group.Select(t => t.Id).ToList();
            var rows = group.Key switch
            {
                RouteTargetTypes.Category => await db.Categories.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, Name = x.Name }).ToListAsync(ct),
                RouteTargetTypes.Product => await db.Products.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, Name = x.Name }).ToListAsync(ct),
                RouteTargetTypes.ContentPage => await db.ContentPages.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, Name = x.Title }).ToListAsync(ct),
                RouteTargetTypes.BlogPost => await db.BlogPosts.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, Name = x.Title }).ToListAsync(ct),
                _ => [],
            };
            foreach (var row in rows)
            {
                result[(group.Key, row.Id)] = row.Name;
            }
        }

        return result;
    }
}
