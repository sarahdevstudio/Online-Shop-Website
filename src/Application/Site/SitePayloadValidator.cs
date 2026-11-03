using System.Text.RegularExpressions;
using Shop.Domain.Common;
using Shop.Domain.Site;

namespace Shop.Application.Site;

/// <summary>The ids a payload refers to; the service checks they exist before validating.</summary>
public sealed record SiteReferenceIds(HashSet<int> MediaIds, HashSet<int> CategoryIds, HashSet<int> ProductIds);

/// <summary>What exists: media id → purpose, category ids, product ids.</summary>
public sealed record SiteReferences(IReadOnlyDictionary<int, string> Media, IReadOnlySet<int> CategoryIds, IReadOnlySet<int> ProductIds)
{
    public static SiteReferences None { get; } = new(new Dictionary<int, string>(), new HashSet<int>(), new HashSet<int>());
}

public sealed record SiteValidationResult(object Payload, IReadOnlyDictionary<string, string[]> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Normalizes and validates one payload (schema, bounds, links, references). Error keys are JSON paths relative to the payload
/// root in camelCase ("blocks[0].data.slides[1].title"); messages are Persian. The returned payload is the normalized one
/// (Persian letters, trimmed text, lower-case colours, normalized social URLs, new ids for blocks without one, unknown block
/// types dropped).
/// </summary>
public static partial class SitePayloadValidator
{
    [GeneratedRegex(@"^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();

    [GeneratedRegex(@"^[0-9]{1,10}$")]
    private static partial Regex Digits();

    [GeneratedRegex(@"^[A-Za-z0-9]{10,64}$")]
    private static partial Regex Code();

    private const string Required = "api.validation.site-required";
    private const string Invalid = "api.validation.site-invalid";
    private const string BadLink = "api.validation.site-link";

    private static string TooLong(int max) => Msg.Of("api.validation.site-max-length", ("max", Fa(max)));

    private static string Fa(int number) => Contrast.Format(number);

    private sealed class Errors
    {
        private readonly Dictionary<string, List<string>> _items = [];

        public void Add(string path, string message)
        {
            if (!_items.TryGetValue(path, out var list))
            {
                _items[path] = list = [];
            }

            list.Add(message);
        }

        public IReadOnlyDictionary<string, string[]> ToDictionary() => _items.ToDictionary(p => p.Key, p => p.Value.ToArray());
    }

    // ---------------------------------------------------------------- references

    public static SiteReferenceIds CollectReferences(object payload)
    {
        var media = new HashSet<int>();
        var categories = new HashSet<int>();
        var products = new HashSet<int>();
        switch (payload)
        {
            case ThemePayload theme:
                AddIfSet(media, theme.Logo?.MediaId);
                AddIfSet(media, theme.Favicon?.MediaId);
                break;
            case SeoPayload seo:
                AddIfSet(media, seo.DefaultOgMediaId);
                break;
            case HomePayload home:
                foreach (var block in home.Blocks)
                {
                    switch (block.Data)
                    {
                        case HeroData hero:
                            foreach (var slide in hero.Slides)
                            {
                                AddIfSet(media, slide.MediaId);
                            }

                            break;
                        case BannerData banner:
                            AddIfSet(media, banner.MediaId);
                            break;
                        case CategoryTilesData tiles:
                            categories.UnionWith(tiles.Items.Select(i => i.CategoryId));
                            break;
                        case ProductRowData row:
                            AddIfSet(categories, row.CategoryId);
                            products.UnionWith(row.ProductIds);
                            break;
                    }
                }

                break;
        }

        return new SiteReferenceIds(media, categories, products);
    }

    private static void AddIfSet(HashSet<int> set, int? id)
    {
        if (id is > 0)
        {
            set.Add(id.Value);
        }
    }

    // ---------------------------------------------------------------- entry point

    public static SiteValidationResult Validate(SiteKind kind, object payload, SiteReferences references)
    {
        var errors = new Errors();
        object normalized = kind switch
        {
            SiteKind.Theme => ValidateTheme((ThemePayload)payload, references, errors),
            SiteKind.Home => ValidateHome((HomePayload)payload, references, errors),
            SiteKind.Chrome => ValidateChrome((ChromePayload)payload, errors),
            SiteKind.Seo => ValidateSeo((SeoPayload)payload, references, errors),
            SiteKind.Badges => ValidateBadges((BadgesPayload)payload, errors),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        return new SiteValidationResult(normalized, errors.ToDictionary());
    }

    // ---------------------------------------------------------------- shared helpers

    private static string Text(string? value) => PersianText.Normalize(value);

    private static string? OptionalText(string? value)
    {
        var text = Text(value);
        return text.Length == 0 ? null : text;
    }

    private static string RequiredText(string? value, int max, string path, Errors errors, bool required = true)
    {
        var text = Text(value);
        if (text.Length == 0 && required)
        {
            errors.Add(path, Required);
        }
        else if (text.Length > max)
        {
            errors.Add(path, TooLong(max));
        }

        return text;
    }

    private static string? OptionalLink(string? value, string path, Errors errors)
    {
        var link = value?.Trim();
        if (string.IsNullOrEmpty(link))
        {
            return null;
        }

        if (!SiteLinks.IsValid(link))
        {
            errors.Add(path, BadLink);
        }

        return link;
    }

    private static string RequiredLink(string? value, string path, Errors errors)
    {
        var link = value?.Trim() ?? string.Empty;
        if (link.Length == 0)
        {
            errors.Add(path, Required);
        }
        else if (!SiteLinks.IsValid(link))
        {
            errors.Add(path, BadLink);
        }

        return link;
    }

    private static void CheckMedia(int? id, string path, SiteReferences refs, Errors errors, string? purpose = null)
    {
        if (id is null or <= 0)
        {
            errors.Add(path, Required);
        }
        else if (!refs.Media.TryGetValue(id.Value, out var actual))
        {
            errors.Add(path, "api.validation.site-image-missing");
        }
        else if (purpose is not null && actual != purpose)
        {
            errors.Add(path, "api.validation.site-image-wrong-purpose");
        }
    }

    // ---------------------------------------------------------------- theme

    private static string ColorOf(string? value, string path, Errors errors)
    {
        var color = value?.Trim() ?? string.Empty;
        if (!HexColor().IsMatch(color))
        {
            errors.Add(path, "api.validation.site-color");
            return color;
        }

        return color.ToLowerInvariant();
    }

    private static ThemePayload ValidateTheme(ThemePayload theme, SiteReferences refs, Errors errors)
    {
        var font = theme.Font?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!SiteValues.Fonts.Contains(font))
        {
            errors.Add("font", "api.validation.site-font");
        }

        var radius = theme.Radius?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!SiteValues.Radii.Contains(radius))
        {
            errors.Add("radius", "api.validation.site-radius");
        }

        ThemeLogo? logo = null;
        if (theme.Logo is not null)
        {
            CheckMedia(theme.Logo.MediaId, "logo.mediaId", refs, errors, "logo");
            logo = new ThemeLogo { MediaId = theme.Logo.MediaId, Alt = RequiredText(theme.Logo.Alt, 100, "logo.alt", errors) };
        }

        ThemeFavicon? favicon = null;
        if (theme.Favicon is not null)
        {
            CheckMedia(theme.Favicon.MediaId, "favicon.mediaId", refs, errors, "favicon");
            favicon = new ThemeFavicon { MediaId = theme.Favicon.MediaId };
        }

        return new ThemePayload
        {
            Primary = ColorOf(theme.Primary, "primary", errors),
            Secondary = ColorOf(theme.Secondary, "secondary", errors),
            Accent = ColorOf(theme.Accent, "accent", errors),
            Background = ColorOf(theme.Background, "background", errors),
            Surface = ColorOf(theme.Surface, "surface", errors),
            Text = ColorOf(theme.Text, "text", errors),
            Success = ColorOf(theme.Success, "success", errors),
            Warning = ColorOf(theme.Warning, "warning", errors),
            Danger = ColorOf(theme.Danger, "danger", errors),
            Font = font,
            Radius = radius,
            Logo = logo,
            Favicon = favicon,
        };
    }

    // ---------------------------------------------------------------- home

    private static HomePayload ValidateHome(HomePayload home, SiteReferences refs, Errors errors)
    {
        // Unknown block types are dropped (forward compatibility); everything left is validated.
        var known = home.Blocks.Where(b => b.Data is not null && SiteValues.BlockTypes.Contains(b.Type)).ToList();
        if (known.Count > SiteValues.MaxBlocks)
        {
            errors.Add("blocks", Msg.Of("api.validation.site-blocks-max", ("max", Fa(SiteValues.MaxBlocks))));
        }

        var seen = new HashSet<Guid>();
        var result = new List<HomeBlock>();
        for (var i = 0; i < known.Count; i++)
        {
            var block = known[i];
            var path = $"blocks[{i}]";
            var id = block.Id == Guid.Empty ? Guid.NewGuid() : block.Id;
            if (!seen.Add(id))
            {
                errors.Add($"{path}.id", "api.validation.site-block-duplicate");
            }

            object data = block.Data switch
            {
                HeroData hero => ValidateHero(hero, $"{path}.data", refs, errors),
                CategoryTilesData tiles => ValidateTiles(tiles, $"{path}.data", refs, errors),
                ProductRowData row => ValidateRow(row, $"{path}.data", refs, errors),
                TextBlockData text => ValidateText(text, $"{path}.data", errors),
                BannerData banner => ValidateBanner(banner, $"{path}.data", refs, errors),
                _ => throw new InvalidOperationException("Unreachable: unknown data type."),
            };
            result.Add(new HomeBlock { Id = id, Type = block.Type, Enabled = block.Enabled, Data = data });
        }

        return new HomePayload { Blocks = result };
    }

    private static HeroData ValidateHero(HeroData hero, string path, SiteReferences refs, Errors errors)
    {
        if (hero.AutoplaySeconds != 0 && hero.AutoplaySeconds is < 4 or > 12)
        {
            errors.Add($"{path}.autoplaySeconds", "api.validation.site-hero-autoplay");
        }

        if (hero.Slides.Count is < 1 or > 6)
        {
            errors.Add($"{path}.slides", "api.validation.site-hero-slides");
        }

        var slides = new List<HeroSlide>();
        for (var i = 0; i < hero.Slides.Count; i++)
        {
            var slide = hero.Slides[i];
            var p = $"{path}.slides[{i}]";
            var title = RequiredText(slide.Title, 80, $"{p}.title", errors, required: false);
            var text = RequiredText(slide.Text, 200, $"{p}.text", errors, required: false);
            var label = RequiredText(slide.LinkLabel, 30, $"{p}.linkLabel", errors, required: false);
            var link = OptionalLink(slide.Link, $"{p}.link", errors);
            if (link is not null && label.Length == 0)
            {
                errors.Add($"{p}.linkLabel", Required);
            }

            var alt = Text(slide.Alt);
            if (slide.MediaId is > 0)
            {
                CheckMedia(slide.MediaId, $"{p}.mediaId", refs, errors);
                if (alt.Length == 0)
                {
                    errors.Add($"{p}.alt", Required);
                }
                else if (alt.Length > SiteMedia.AltMaxLength)
                {
                    errors.Add($"{p}.alt", TooLong(SiteMedia.AltMaxLength));
                }
            }
            else if (title.Length == 0)
            {
                errors.Add($"{p}.title", "api.validation.site-slide-content");
            }

            slides.Add(new HeroSlide
            {
                MediaId = slide.MediaId is > 0 ? slide.MediaId : null,
                Alt = alt,
                Title = title,
                Text = text,
                LinkLabel = label,
                Link = link,
            });
        }

        return new HeroData { AutoplaySeconds = hero.AutoplaySeconds, Slides = slides };
    }

    private static CategoryTilesData ValidateTiles(CategoryTilesData tiles, string path, SiteReferences refs, Errors errors)
    {
        if (tiles.Items.Count is < 2 or > 12)
        {
            errors.Add($"{path}.items", "api.validation.site-tiles-count");
        }

        var seen = new HashSet<int>();
        var items = new List<CategoryTileItem>();
        for (var i = 0; i < tiles.Items.Count; i++)
        {
            var item = tiles.Items[i];
            var p = $"{path}.items[{i}]";
            if (!refs.CategoryIds.Contains(item.CategoryId))
            {
                errors.Add($"{p}.categoryId", "api.validation.site-category-missing");
            }
            else if (!seen.Add(item.CategoryId))
            {
                errors.Add($"{p}.categoryId", "api.validation.site-category-duplicate");
            }

            var label = OptionalText(item.Label);
            if (label is { Length: > 40 })
            {
                errors.Add($"{p}.label", TooLong(40));
            }

            items.Add(new CategoryTileItem { CategoryId = item.CategoryId, Label = label });
        }

        return new CategoryTilesData { Items = items };
    }

    private static ProductRowData ValidateRow(ProductRowData row, string path, SiteReferences refs, Errors errors)
    {
        var title = RequiredText(row.Title, 60, $"{path}.title", errors);
        var source = row.Source?.Trim() ?? string.Empty;
        if (!SiteValues.ProductSources.Contains(source))
        {
            errors.Add($"{path}.source", "api.validation.site-row-source");
        }

        if (row.Limit is < 4 or > 12)
        {
            errors.Add($"{path}.limit", "api.validation.site-row-limit");
        }

        int? categoryId = null;
        var productIds = new List<int>();
        if (source == "category")
        {
            if (row.CategoryId is null || !refs.CategoryIds.Contains(row.CategoryId.Value))
            {
                errors.Add($"{path}.categoryId", "api.validation.site-category-missing");
            }

            categoryId = row.CategoryId;
        }
        else if (source == "handpicked")
        {
            productIds = row.ProductIds.Distinct().ToList();
            if (productIds.Count is < 1 or > 12)
            {
                errors.Add($"{path}.productIds", "api.validation.site-row-handpicked-count");
            }

            if (productIds.Any(id => !refs.ProductIds.Contains(id)))
            {
                errors.Add($"{path}.productIds", "api.validation.site-row-product-missing");
            }
        }

        return new ProductRowData { Title = title, Source = source, CategoryId = categoryId, ProductIds = productIds, Limit = row.Limit };
    }

    private static TextBlockData ValidateText(TextBlockData data, string path, Errors errors)
    {
        var heading = RequiredText(data.Heading, 80, $"{path}.heading", errors, required: false);
        var body = PersianText.NormalizeLetters(data.Body ?? string.Empty).Trim();
        if (body.Length > 3000)
        {
            errors.Add($"{path}.body", TooLong(3000));
        }

        if (heading.Length == 0 && body.Length == 0)
        {
            errors.Add($"{path}.body", Required);
        }

        return new TextBlockData { Heading = heading, Body = body };
    }

    private static BannerData ValidateBanner(BannerData data, string path, SiteReferences refs, Errors errors)
    {
        CheckMedia(data.MediaId, $"{path}.mediaId", refs, errors);
        var alt = RequiredText(data.Alt, SiteMedia.AltMaxLength, $"{path}.alt", errors);
        var link = OptionalLink(data.Link, $"{path}.link", errors);
        var height = data.Height?.Trim() ?? string.Empty;
        if (!SiteValues.BannerHeights.Contains(height))
        {
            errors.Add($"{path}.height", "api.validation.site-banner-height");
        }

        return new BannerData { MediaId = data.MediaId, Alt = alt, Link = link, Height = height };
    }

    // ---------------------------------------------------------------- chrome

    private static ChromePayload ValidateChrome(ChromePayload chrome, Errors errors)
    {
        var bar = chrome.Announcement;
        var style = bar.Style?.Trim() ?? string.Empty;
        if (!SiteValues.AnnouncementStyles.Contains(style))
        {
            errors.Add("announcement.style", "api.validation.site-announcement-style");
        }

        var barText = RequiredText(bar.Text, 140, "announcement.text", errors, required: bar.Enabled);
        var announcement = new AnnouncementBar
        {
            Enabled = bar.Enabled,
            Text = barText,
            Link = OptionalLink(bar.Link, "announcement.link", errors),
            Style = style,
            Dismissible = bar.Dismissible,
        };

        if (chrome.HeaderMenu.Count > 8)
        {
            errors.Add("headerMenu", Msg.Of("api.validation.site-menu-max", ("max", Fa(8))));
        }

        var menu = new List<MenuItem>();
        for (var i = 0; i < chrome.HeaderMenu.Count; i++)
        {
            var item = chrome.HeaderMenu[i];
            var p = $"headerMenu[{i}]";
            var label = RequiredText(item.Label, 30, $"{p}.label", errors);
            var link = OptionalLink(item.Link, $"{p}.link", errors);
            var children = new List<MenuChild>();
            if (!item.AllCategories)
            {
                if (item.Children.Count > 8)
                {
                    errors.Add($"{p}.children", Msg.Of("api.validation.site-submenu-max", ("max", Fa(8))));
                }

                for (var c = 0; c < item.Children.Count; c++)
                {
                    var child = item.Children[c];
                    children.Add(new MenuChild
                    {
                        Label = RequiredText(child.Label, 30, $"{p}.children[{c}].label", errors),
                        Link = RequiredLink(child.Link, $"{p}.children[{c}].link", errors),
                    });
                }
            }

            if (link is null && children.Count == 0 && !item.AllCategories)
            {
                errors.Add($"{p}.link", Required);
            }

            menu.Add(new MenuItem { Label = label, Link = link, AllCategories = item.AllCategories, Children = children });
        }

        var about = RequiredText(chrome.Footer.About, 300, "footer.about", errors, required: false);
        if (chrome.Footer.Columns.Count > 4)
        {
            errors.Add("footer.columns", Msg.Of("api.validation.site-footer-columns-max", ("max", Fa(4))));
        }

        var columns = new List<FooterColumn>();
        for (var i = 0; i < chrome.Footer.Columns.Count; i++)
        {
            var column = chrome.Footer.Columns[i];
            var p = $"footer.columns[{i}]";
            var title = RequiredText(column.Title, 40, $"{p}.title", errors);
            if (column.Links.Count > 8)
            {
                errors.Add($"{p}.links", Msg.Of("api.validation.site-footer-links-max", ("max", Fa(8))));
            }

            var links = column.Links.Select((link, l) => new FooterLink
            {
                Label = RequiredText(link.Label, 30, $"{p}.links[{l}].label", errors),
                Link = RequiredLink(link.Link, $"{p}.links[{l}].link", errors),
            }).ToList();
            columns.Add(new FooterColumn { Title = title, Links = links });
        }

        var social = chrome.Social;
        return new ChromePayload
        {
            Announcement = announcement,
            HeaderMenu = menu,
            Footer = new FooterContent { About = about, Columns = columns },
            Social = new SocialLinks
            {
                Telegram = Social("telegram", social.Telegram, errors),
                Whatsapp = Social("whatsapp", social.Whatsapp, errors),
                Aparat = Social("aparat", social.Aparat, errors),
                Twitter = Social("twitter", social.Twitter, errors),
                Pinterest = Social("pinterest", social.Pinterest, errors),
                Youtube = Social("youtube", social.Youtube, errors),
                Linkedin = Social("linkedin", social.Linkedin, errors),
            },
        };
    }

    private static string? Social(string platform, string? input, Errors errors)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var normalized = SocialLinkNormalizer.Normalize(platform, input);
        if (normalized is null)
        {
            errors.Add($"social.{platform}", "api.validation.site-social");
        }

        return normalized;
    }

    // ---------------------------------------------------------------- seo

    private static SeoPayload ValidateSeo(SeoPayload seo, SiteReferences refs, Errors errors)
    {
        var siteName = Text(seo.SiteName);
        if (siteName.Length is < 2 or > 60)
        {
            errors.Add("siteName", "api.validation.site-name-length");
        }

        var template = Text(seo.TitleTemplate);
        if (template.Length > 70)
        {
            errors.Add("titleTemplate", TooLong(70));
        }
        else if (template.Split("%s").Length != 2)
        {
            errors.Add("titleTemplate", "api.validation.site-title-template");
        }

        var homeTitle = RequiredText(seo.HomeTitle, 70, "homeTitle", errors);
        var description = RequiredText(seo.DefaultDescription, 160, "defaultDescription", errors, required: false);
        int? og = seo.DefaultOgMediaId is > 0 ? seo.DefaultOgMediaId : null;
        if (og is not null)
        {
            CheckMedia(og, "defaultOgMediaId", refs, errors);
        }

        return new SeoPayload { SiteName = siteName, TitleTemplate = template, HomeTitle = homeTitle, DefaultDescription = description, DefaultOgMediaId = og };
    }

    // ---------------------------------------------------------------- badges

    private static BadgesPayload ValidateBadges(BadgesPayload badges, Errors errors)
    {
        EnamadBadge? enamad = null;
        if (badges.Enamad is { } e && !(IsBlank(e.Id) && IsBlank(e.Code)))
        {
            enamad = new EnamadBadge { Id = e.Id?.Trim() ?? string.Empty, Code = e.Code?.Trim() ?? string.Empty };
            if (!Digits().IsMatch(enamad.Id))
            {
                errors.Add("enamad.id", "api.validation.site-badge-id");
            }

            if (!Code().IsMatch(enamad.Code))
            {
                errors.Add("enamad.code", "api.validation.site-badge-code");
            }
        }

        SamandehiBadge? samandehi = null;
        if (badges.Samandehi is { } s && !(IsBlank(s.Id) && IsBlank(s.VerifyCode) && IsBlank(s.LogoCode)))
        {
            samandehi = new SamandehiBadge
            {
                Id = s.Id?.Trim() ?? string.Empty,
                VerifyCode = s.VerifyCode?.Trim() ?? string.Empty,
                LogoCode = s.LogoCode?.Trim() ?? string.Empty,
            };
            if (!Digits().IsMatch(samandehi.Id))
            {
                errors.Add("samandehi.id", "api.validation.site-badge-id");
            }

            if (!Code().IsMatch(samandehi.VerifyCode))
            {
                errors.Add("samandehi.verifyCode", "api.validation.site-badge-code");
            }

            if (!Code().IsMatch(samandehi.LogoCode))
            {
                errors.Add("samandehi.logoCode", "api.validation.site-badge-code");
            }
        }

        return new BadgesPayload { Enamad = enamad, Samandehi = samandehi };
    }

    private static bool IsBlank(string? value) => string.IsNullOrWhiteSpace(value);
}
