using Microsoft.EntityFrameworkCore;
using Shop.Application.Routing;
using Shop.Application.Site;
using Shop.Domain.Common;
using Shop.Domain.Routing;
using Shop.Infrastructure.Persistence;

namespace Shop.Infrastructure.Site;

/// <summary>The searchable list behind the editor's "link to an entity" select, and the labels of links already stored.</summary>
internal sealed class SiteLinkTargets(ShopDbContext db, IRouteTable routes) : ISiteLinkTargets
{
    private const int Limit = 20;

    public async Task<IReadOnlyList<SiteLinkTargetDto>> SearchAsync(string type, string? query, CancellationToken ct)
    {
        var term = PersianText.Normalize(query);
        var rows = type switch
        {
            RouteTargetTypes.Category => await db.Categories.AsNoTracking().Where(x => term == "" || x.Name.Contains(term)).OrderBy(x => x.Name).Take(Limit).Select(x => new Row(x.Id, x.Name)).ToListAsync(ct),
            RouteTargetTypes.Product => await db.Products.AsNoTracking().Where(x => term == "" || x.Name.Contains(term)).OrderBy(x => x.Name).Take(Limit).Select(x => new Row(x.Id, x.Name)).ToListAsync(ct),
            RouteTargetTypes.ContentPage => await db.ContentPages.AsNoTracking().Where(x => term == "" || x.Title.Contains(term)).OrderBy(x => x.Title).Take(Limit).Select(x => new Row(x.Id, x.Title)).ToListAsync(ct),
            RouteTargetTypes.BlogPost => await db.BlogPosts.AsNoTracking().Where(x => term == "" || x.Title.Contains(term)).OrderByDescending(x => x.Id).Take(Limit).Select(x => new Row(x.Id, x.Title)).ToListAsync(ct),
            _ => [],
        };
        return rows.Select(r => new SiteLinkTargetDto(type, r.Id, r.Label, routes.CurrentPathOf(type, r.Id), true)).ToList();
    }

    public async Task<IReadOnlyDictionary<string, SiteLinkTargetDto>> DescribeAsync(IEnumerable<(string Type, int Id)> entities, CancellationToken ct)
    {
        var result = new Dictionary<string, SiteLinkTargetDto>();
        foreach (var group in entities.Distinct().GroupBy(e => e.Type, e => e.Id))
        {
            var ids = group.ToList();
            var rows = group.Key switch
            {
                RouteTargetTypes.Category => await db.Categories.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => new Row(x.Id, x.Name)).ToListAsync(ct),
                RouteTargetTypes.Product => await db.Products.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => new Row(x.Id, x.Name)).ToListAsync(ct),
                RouteTargetTypes.ContentPage => await db.ContentPages.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => new Row(x.Id, x.Title)).ToListAsync(ct),
                RouteTargetTypes.BlogPost => await db.BlogPosts.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => new Row(x.Id, x.Title)).ToListAsync(ct),
                _ => [],
            };
            foreach (var id in ids)
            {
                var row = rows.FirstOrDefault(r => r.Id == id);
                result[$"{group.Key}:{id}"] = new SiteLinkTargetDto(group.Key, id, row?.Label ?? string.Empty, row is null ? null : routes.CurrentPathOf(group.Key, id), row is not null);
            }
        }

        return result;
    }

    private sealed record Row(int Id, string Label);
}
