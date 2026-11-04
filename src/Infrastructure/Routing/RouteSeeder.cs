using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shop.Domain.Routing;
using Shop.Infrastructure.Persistence;

namespace Shop.Infrastructure.Routing;

/// <summary>
/// Gives every existing category, product, info page and blog post its CURRENT URL as the first route row (docs/11 C.1 #3), so
/// switching dynamic routing on moves nothing. Idempotent: only entities without any row are touched, and it runs at every start
/// (also heals an entity created by a path that skipped the route service).
/// </summary>
public sealed class RouteSeeder(ShopDbContext db, RouteTable table, TimeProvider clock, ILogger<RouteSeeder> logger)
{
    public async Task EnsureAsync(CancellationToken ct)
    {
        var have = (await db.RoutePaths.AsNoTracking().Select(r => new { r.TargetType, r.TargetId }).Distinct().ToListAsync(ct))
            .Select(r => (r.TargetType, r.TargetId)).ToHashSet();
        var taken = (await db.RoutePaths.AsNoTracking().Select(r => r.Path).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var now = clock.GetUtcNow().UtcDateTime;

        var wanted = new List<(string Type, int Id, string Slug)>();
        wanted.AddRange((await db.Categories.AsNoTracking().Select(x => new { x.Id, x.Slug }).ToListAsync(ct)).Select(x => (RouteTargetTypes.Category, x.Id, x.Slug)));
        wanted.AddRange((await db.Products.AsNoTracking().Select(x => new { x.Id, x.Slug }).ToListAsync(ct)).Select(x => (RouteTargetTypes.Product, x.Id, x.Slug)));
        wanted.AddRange((await db.ContentPages.AsNoTracking().Select(x => new { x.Id, x.Slug }).ToListAsync(ct)).Select(x => (RouteTargetTypes.ContentPage, x.Id, x.Slug)));
        wanted.AddRange((await db.BlogPosts.AsNoTracking().Select(x => new { x.Id, x.Slug }).ToListAsync(ct)).Select(x => (RouteTargetTypes.BlogPost, x.Id, x.Slug)));

        var added = 0;
        foreach (var (type, id, slug) in wanted.Where(w => !have.Contains((w.Type, w.Id))))
        {
            var path = RouteTargetTypes.DefaultPath(type, slug);
            if (!taken.Add(path))
            {
                logger.LogWarning("Route for {Type} {Id} not created: {Path} is already taken", type, id, path);
                continue;
            }

            db.RoutePaths.Add(RouteEntry.Create(path, type, id, true, null, now));
            added++;
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Created {Count} route paths from existing slugs", added);
        }

        await table.ReloadAsync(ct);
    }
}
