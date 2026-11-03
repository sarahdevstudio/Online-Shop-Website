using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Shop.Application.Catalog;
using Shop.Application.Site;
using Shop.Domain.Catalog;
using Shop.Domain.Site;
using Shop.Infrastructure.Persistence;

namespace Shop.Infrastructure.Site;

/// <summary>
/// Turns the stored payloads into the document the storefront reads: media ids become URLs, category and product ids become
/// slugs, disabled blocks and anything that can no longer be resolved are left out. A kind without a usable version falls back
/// to the built-in defaults, so the document is always complete.
/// </summary>
internal sealed class SiteCompiler(ShopDbContext db, IMediaStorage media)
{
    public async Task<SiteConfigDto> CompileAsync(bool useDrafts, CancellationToken ct)
    {
        var rows = await db.SiteConfigVersions.AsNoTracking()
            .Where(v => v.State == SiteState.Published || (useDrafts && v.State == SiteState.Draft))
            .ToListAsync(ct);

        object PayloadOf(SiteKind kind)
        {
            var row = (useDrafts ? rows.FirstOrDefault(r => r.Kind == kind && r.State == SiteState.Draft) : null)
                ?? rows.FirstOrDefault(r => r.Kind == kind && r.State == SiteState.Published);
            return (row is null ? null : SiteJson.TryDeserialize(kind, row.Payload)) ?? SiteDefaults.ForKind(kind);
        }

        var theme = (ThemePayload)PayloadOf(SiteKind.Theme);
        var home = (HomePayload)PayloadOf(SiteKind.Home);
        var chrome = (ChromePayload)PayloadOf(SiteKind.Chrome);
        var seo = (SeoPayload)PayloadOf(SiteKind.Seo);
        var badges = (BadgesPayload)PayloadOf(SiteKind.Badges);

        var mediaIds = new HashSet<int>();
        foreach (var payload in new object[] { theme, home, seo })
        {
            mediaIds.UnionWith(SitePayloadValidator.CollectReferences(payload).MediaIds);
        }

        var files = await db.SiteMedia.AsNoTracking().Where(m => mediaIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, ct);

        SiteImageRefDto? Image(int? id) =>
            id is { } key && files.TryGetValue(key, out var file) ? new SiteImageRefDto(media.GetUrl(file.Path), file.Width, file.Height) : null;

        var themeDto = new SiteThemeDto(
            theme.Primary, theme.Secondary, theme.Accent, theme.Background, theme.Surface, theme.Text, theme.Success, theme.Warning, theme.Danger,
            theme.Font, theme.Radius,
            theme.Logo is { } logo && Image(logo.MediaId) is { } logoImage ? new SiteLogoDto(logoImage.Url, logo.Alt, logoImage.Width, logoImage.Height) : null,
            Image(theme.Favicon?.MediaId));

        var seoDto = new SiteSeoDto(seo.SiteName, seo.TitleTemplate, seo.HomeTitle, seo.DefaultDescription, Image(seo.DefaultOgMediaId));

        var social = new Dictionary<string, string>();
        void AddSocial(string key, string? url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                social[key] = url;
            }
        }

        AddSocial("telegram", chrome.Social.Telegram);
        AddSocial("whatsapp", chrome.Social.Whatsapp);
        AddSocial("aparat", chrome.Social.Aparat);
        AddSocial("twitter", chrome.Social.Twitter);
        AddSocial("pinterest", chrome.Social.Pinterest);
        AddSocial("youtube", chrome.Social.Youtube);
        AddSocial("linkedin", chrome.Social.Linkedin);
        var chromeDto = new SiteChromeDto(chrome.Announcement, chrome.HeaderMenu, chrome.Footer, social);

        return new SiteConfigDto(VersionOf(rows, useDrafts), themeDto, await CompileHomeAsync(home, Image, ct), chromeDto, seoDto, badges);
    }

    private async Task<SiteHomeDto> CompileHomeAsync(HomePayload home, Func<int?, SiteImageRefDto?> image, CancellationToken ct)
    {
        var categories = await db.Categories.AsNoTracking().Where(c => c.IsActive).ToListAsync(ct);
        var byId = categories.ToDictionary(c => c.Id);
        var blocks = new List<SiteBlockDto>();

        foreach (var block in home.Blocks.Where(b => b.Enabled && b.Data is not null))
        {
            object? data = block.Data switch
            {
                HeroData hero => CompileHero(hero, image),
                CategoryTilesData tiles => await CompileTilesAsync(tiles, categories, byId, ct),
                ProductRowData row => await CompileRowAsync(row, byId, ct),
                TextBlockData text => new SiteTextDataDto(text.Heading, text.Body),
                BannerData banner => image(banner.MediaId) is { } bannerImage
                    ? new SiteBannerDataDto(bannerImage, banner.Alt, banner.Link, banner.Height)
                    : null,
                _ => null,
            };
            if (data is not null)
            {
                blocks.Add(new SiteBlockDto(block.Id, block.Type, data));
            }
        }

        return new SiteHomeDto(blocks);
    }

    private static SiteHeroDataDto? CompileHero(HeroData hero, Func<int?, SiteImageRefDto?> image)
    {
        var slides = new List<SiteHeroSlideDto>();
        foreach (var slide in hero.Slides)
        {
            var picture = image(slide.MediaId);
            // A slide whose image is gone still works as a text slide when it has a title.
            if (picture is null && string.IsNullOrEmpty(slide.Title))
            {
                continue;
            }

            slides.Add(new SiteHeroSlideDto(picture, slide.Alt, slide.Title, slide.Text, slide.LinkLabel, slide.Link));
        }

        return slides.Count == 0 ? null : new SiteHeroDataDto(hero.AutoplaySeconds, slides);
    }

    private async Task<SiteCategoryTilesDataDto?> CompileTilesAsync(
        CategoryTilesData tiles, List<Category> categories, Dictionary<int, Category> byId, CancellationToken ct)
    {
        var items = new List<SiteCategoryTileDto>();
        foreach (var item in tiles.Items)
        {
            if (!byId.TryGetValue(item.CategoryId, out var category))
            {
                continue;
            }

            var ids = categories.Where(c => c.Id == category.Id || c.ParentId == category.Id).Select(c => c.Id).ToList();
            var imagePath = await db.Products.AsNoTracking()
                .Where(p => p.Status == ProductStatus.Published && ids.Contains(p.CategoryId))
                .OrderByDescending(p => p.CreatedAtUtc)
                .Select(p => p.Images.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).Select(i => i.Path).FirstOrDefault())
                .Where(imagePath => imagePath != null)
                .FirstOrDefaultAsync(ct);
            items.Add(new SiteCategoryTileDto(string.IsNullOrEmpty(item.Label) ? category.Name : item.Label, category.Slug, imagePath is null ? null : media.GetUrl(imagePath)));
        }

        return items.Count < 2 ? null : new SiteCategoryTilesDataDto(items);
    }

    private async Task<SiteProductRowDataDto?> CompileRowAsync(ProductRowData row, Dictionary<int, Category> byId, CancellationToken ct)
    {
        switch (row.Source)
        {
            case "category":
                return row.CategoryId is { } id && byId.TryGetValue(id, out var category)
                    ? new SiteProductRowDataDto(row.Title, row.Source, row.Limit, category.Slug, [], $"/category/{category.Slug}")
                    : null;
            case "handpicked":
                var found = await db.Products.AsNoTracking()
                    .Where(p => row.ProductIds.Contains(p.Id) && p.Status == ProductStatus.Published && p.Variants.Any(v => v.IsActive))
                    .Select(p => new { p.Id, p.Slug })
                    .ToListAsync(ct);
                var slugs = row.ProductIds.Select(pid => found.FirstOrDefault(f => f.Id == pid)?.Slug).Where(s => s is not null).Select(s => s!).ToList();
                return slugs.Count == 0 ? null : new SiteProductRowDataDto(row.Title, row.Source, row.Limit, null, slugs, null);
            default:
                return new SiteProductRowDataDto(row.Title, row.Source, row.Limit, null, [], null);
        }
    }

    /// <summary>A short hash that changes whenever a published version (or, for the preview, a draft) changes.</summary>
    private static string VersionOf(List<SiteConfigVersion> rows, bool drafts)
    {
        var text = string.Join(';', rows.OrderBy(r => r.Kind).ThenBy(r => r.State)
            .Select(r => $"{r.Kind.ToRoute()}:{r.State.ToDb()}:{r.Version}:{r.Id}:{r.UpdatedAtUtc.Ticks}"));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12].ToLowerInvariant();
        return drafts ? "draft-" + hash : hash;
    }
}
