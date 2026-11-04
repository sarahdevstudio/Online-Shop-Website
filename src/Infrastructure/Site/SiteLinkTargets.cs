using Microsoft.EntityFrameworkCore;
using Shop.Application.Routing;
using Shop.Application.Site;
using Shop.Domain.Catalog;
using Shop.Domain.Common;
using Shop.Domain.Content;
using Shop.Domain.Routing;
using Shop.Infrastructure.Persistence;

namespace Shop.Infrastructure.Site;

/// <summary>
/// The searchable list behind the editor's "link to an entity" select, and the name, path and state of links already stored.
/// The state is read from the database, so it is always current (the compiler asks for it too: a link to anything that is not
/// published is left off the site).
/// </summary>
internal sealed class SiteLinkTargets(ShopDbContext db, IRouteTable routes) : ISiteLinkTargets
{
    private const int Limit = 20;

    public async Task<IReadOnlyList<SiteLinkTargetDto>> SearchAsync(string type, string? query, CancellationToken ct)
    {
        var term = PersianText.Normalize(query);
        var rows = type switch
        {
            RouteTargetTypes.Category => await db.Categories.AsNoTracking().Where(x => term == "" || x.Name.Contains(term)).OrderBy(x => x.Name).Take(Limit)
                .Select(x => new Row(x.Id, x.Name, x.IsActive ? LinkStates.Published : LinkStates.Inactive)).ToListAsync(ct),
            RouteTargetTypes.Product => await db.Products.AsNoTracking().Where(x => term == "" || x.Name.Contains(term)).OrderBy(x => x.Name).Take(Limit)
                .Select(x => new Row(x.Id, x.Name, x.Status == ProductStatus.Published ? LinkStates.Published : x.Status == ProductStatus.Archived ? LinkStates.Archived : LinkStates.Draft)).ToListAsync(ct),
            RouteTargetTypes.ContentPage => await db.ContentPages.AsNoTracking().Where(x => term == "" || x.Title.Contains(term)).OrderBy(x => x.Title).Take(Limit)
                .Select(x => new Row(x.Id, x.Title, x.Body.Contains(ContentPage.DraftMarker) ? LinkStates.Draft : LinkStates.Published)).ToListAsync(ct),
            RouteTargetTypes.BlogPost => await db.BlogPosts.AsNoTracking().Where(x => term == "" || x.Title.Contains(term)).OrderByDescending(x => x.Id).Take(Limit)
                .Select(x => new Row(x.Id, x.Title, x.IsPublished ? LinkStates.Published : LinkStates.Draft)).ToListAsync(ct),
            _ => [],
        };
        return rows.Select(r => new SiteLinkTargetDto(type, r.Id, r.Label, routes.CurrentPathOf(type, r.Id), true, r.State)).ToList();
    }

    public async Task<IReadOnlyDictionary<string, SiteLinkTargetDto>> DescribeAsync(IEnumerable<(string Type, int Id)> entities, CancellationToken ct)
    {
        var result = new Dictionary<string, SiteLinkTargetDto>();
        foreach (var group in entities.Distinct().GroupBy(e => e.Type, e => e.Id))
        {
            var ids = group.ToList();
            var rows = group.Key switch
            {
                RouteTargetTypes.Category => await db.Categories.AsNoTracking().Where(x => ids.Contains(x.Id))
                    .Select(x => new Row(x.Id, x.Name, x.IsActive ? LinkStates.Published : LinkStates.Inactive)).ToListAsync(ct),
                RouteTargetTypes.Product => await db.Products.AsNoTracking().Where(x => ids.Contains(x.Id))
                    .Select(x => new Row(x.Id, x.Name, x.Status == ProductStatus.Published ? LinkStates.Published : x.Status == ProductStatus.Archived ? LinkStates.Archived : LinkStates.Draft)).ToListAsync(ct),
                RouteTargetTypes.ContentPage => await db.ContentPages.AsNoTracking().Where(x => ids.Contains(x.Id))
                    .Select(x => new Row(x.Id, x.Title, x.Body.Contains(ContentPage.DraftMarker) ? LinkStates.Draft : LinkStates.Published)).ToListAsync(ct),
                RouteTargetTypes.BlogPost => await db.BlogPosts.AsNoTracking().Where(x => ids.Contains(x.Id))
                    .Select(x => new Row(x.Id, x.Title, x.IsPublished ? LinkStates.Published : LinkStates.Draft)).ToListAsync(ct),
                _ => [],
            };
            foreach (var id in ids)
            {
                var row = rows.FirstOrDefault(r => r.Id == id);
                result[$"{group.Key}:{id}"] = row is null
                    ? new SiteLinkTargetDto(group.Key, id, string.Empty, null, false, LinkStates.Deleted)
                    : new SiteLinkTargetDto(group.Key, id, row.Label, routes.CurrentPathOf(group.Key, id), true, row.State);
            }
        }

        return result;
    }

    private sealed record Row(int Id, string Label, string State);
}
