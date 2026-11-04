using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shop.Domain.Routing;
using Shop.Domain.Site;
using Shop.Infrastructure.Persistence;

namespace Shop.Infrastructure.Site;

/// <summary>
/// One-time conversion at the first start after entity links exist: a typed internal link that is exactly the current OR an old
/// path of a category, product, info page or blog post becomes a reference to that entity (so it follows the entity's path
/// from now on); every other link stays a custom URL. All stored versions (draft, published, history) are converted, so a
/// restore brings back entity links too. It runs once: a SiteAuditLog row (action "converted") marks it done, so a custom
/// link an admin types later is never rewritten behind their back.
/// </summary>
public sealed class SiteLinkConverter(ShopDbContext db, TimeProvider clock, ILogger<SiteLinkConverter> logger)
{
    /// <summary>The number of links converted (0 when the conversion already ran).</summary>
    public async Task<int> EnsureAsync(CancellationToken ct)
    {
        if (await db.SiteAuditLogs.AnyAsync(a => a.Action == SiteAuditAction.Converted, ct))
        {
            return 0;
        }

        var rows = await db.RoutePaths.AsNoTracking().Where(r => !r.IsGone).Select(r => new { r.Path, r.TargetType, r.TargetId }).ToListAsync(ct);
        // The names are stored with the reference, so the editor can still name a target after it is deleted.
        var labels = new Dictionary<(string, int), string>();
        foreach (var x in await db.Categories.AsNoTracking().Select(x => new { x.Id, Label = x.Name }).ToListAsync(ct)) labels[(RouteTargetTypes.Category, x.Id)] = x.Label;
        foreach (var x in await db.Products.AsNoTracking().Select(x => new { x.Id, Label = x.Name }).ToListAsync(ct)) labels[(RouteTargetTypes.Product, x.Id)] = x.Label;
        foreach (var x in await db.ContentPages.AsNoTracking().Select(x => new { x.Id, Label = x.Title }).ToListAsync(ct)) labels[(RouteTargetTypes.ContentPage, x.Id)] = x.Label;
        foreach (var x in await db.BlogPosts.AsNoTracking().Select(x => new { x.Id, Label = x.Title }).ToListAsync(ct)) labels[(RouteTargetTypes.BlogPost, x.Id)] = x.Label;

        var byPath = new Dictionary<string, LinkEntity>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rows)
        {
            byPath[r.Path] = new LinkEntity { Type = r.TargetType, Id = r.TargetId, Label = labels.GetValueOrDefault((r.TargetType, r.TargetId)) };
        }

        var converter = new Converter(byPath);
        var versions = await db.SiteConfigVersions.Where(v => v.Kind == SiteKind.Chrome || v.Kind == SiteKind.Home).ToListAsync(ct);
        var total = 0;
        foreach (var version in versions)
        {
            if (SiteJson.TryDeserialize(version.Kind, version.Payload) is not { } payload)
            {
                continue;
            }

            converter.Count = 0;
            object converted = payload switch
            {
                ChromePayload chrome => converter.Chrome(chrome),
                HomePayload home => converter.Home(home),
                _ => payload,
            };
            if (converter.Count > 0)
            {
                version.ReplacePayloadForConversion(SiteJson.Serialize(converted));
                // The count reported is the links of the CURRENT versions (draft and published), not of the history copies.
                if (version.State != SiteState.Archived)
                {
                    total += converter.Count;
                }
            }
        }

        db.SiteAuditLogs.Add(SiteAuditLog.Create(SiteKind.Chrome, SiteAuditAction.Converted, null, $"links converted to entity links: {total}", null, clock.GetUtcNow().UtcDateTime));
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Converted {Count} typed links to entity links", total);
        return total;
    }

    private sealed class Converter(Dictionary<string, LinkEntity> byPath)
    {
        public int Count { get; set; }

        /// <summary>A bare internal path (no query or fragment) that is a known current or old path.</summary>
        private LinkEntity? Match(string? link)
        {
            if (string.IsNullOrEmpty(link) || link[0] != '/' || link.AsSpan().IndexOfAny('?', '#') >= 0)
            {
                return null;
            }

            var normalized = RoutePath.Normalize(link);
            return normalized.Length > 0 && byPath.TryGetValue(normalized, out var entity) ? entity : null;
        }

        private (string? Link, LinkEntity? Entity) Convert(string? link, LinkEntity? entity)
        {
            if (entity is not null || Match(link) is not { } match)
            {
                return (link, entity);
            }

            Count++;
            return (null, match);
        }

        public ChromePayload Chrome(ChromePayload chrome)
        {
            var bar = chrome.Announcement;
            var (barLink, barEntity) = Convert(bar.Link, bar.Entity);
            var menu = chrome.HeaderMenu.Select(item =>
            {
                var (link, entity) = Convert(item.Link, item.Entity);
                var children = item.Children.Select(c =>
                {
                    var (childLink, childEntity) = Convert(c.Link, c.Entity);
                    return c with { Link = childLink ?? string.Empty, Entity = childEntity };
                }).ToList();
                return item with { Link = link, Entity = entity, Children = children };
            }).ToList();
            var columns = chrome.Footer.Columns.Select(column => column with
            {
                Links = column.Links.Select(l =>
                {
                    var (link, entity) = Convert(l.Link, l.Entity);
                    return l with { Link = link ?? string.Empty, Entity = entity };
                }).ToList(),
            }).ToList();
            return chrome with
            {
                Announcement = bar with { Link = barLink, Entity = barEntity },
                HeaderMenu = menu,
                Footer = chrome.Footer with { Columns = columns },
            };
        }

        private BannerData ConvertBanner(BannerData banner)
        {
            var (link, entity) = Convert(banner.Link, banner.Entity);
            return banner with { Link = link, Entity = entity };
        }

        public HomePayload Home(HomePayload home) => home with
        {
            Blocks = home.Blocks.Select(block => block.Data switch
            {
                HeroData hero => block with
                {
                    Data = hero with
                    {
                        Slides = hero.Slides.Select(slide =>
                        {
                            var (link, entity) = Convert(slide.Link, slide.Entity);
                            return slide with { Link = link, Entity = entity };
                        }).ToList(),
                    },
                },
                BannerData banner => block with { Data = ConvertBanner(banner) },
                _ => block,
            }).ToList(),
        };
    }
}
