using Shop.Domain.Routing;
using Shop.Domain.Site;

namespace Shop.Infrastructure.Site;

/// <summary>
/// The built-in site configuration: what the seed publishes the first time and what the compiler falls back to when a kind has
/// no usable version. Business facts come from the shop's previous site (docs/10 §12); anything not found there is left empty.
/// </summary>
public static class SiteDefaults
{
    public const string ShopName = "قصر روشنایی";

    public const string PromisesMarkdown = """
        - **ضمانت کیفیت:** ضمانت حداقل یک‌ساله و ۳ سال خدمات پس از فروش.
        - **نصب رایگان:** در شهرهایی که شعبه داریم.
        - **ارسال رایگان:** برای سفارش‌های بالای ۳ میلیون تومان در شهرهای دارای شعبه.
        """;

    public static ThemePayload Theme(int? logoMediaId = null, int? faviconMediaId = null) => new()
    {
        Primary = "#5b0057",
        Secondary = "#76007c",
        Accent = "#d8c831",
        Background = "#ffffff",
        Surface = "#f2f2f2",
        Text = "#1a1a1a",
        Success = "#1b7f3b",
        Warning = "#8a5a00",
        Danger = "#b3261e",
        Font = "vazirmatn",
        Radius = "sm",
        Logo = logoMediaId is { } logo ? new ThemeLogo { MediaId = logo, Alt = ShopName } : null,
        Favicon = faviconMediaId is { } favicon ? new ThemeFavicon { MediaId = favicon } : null,
    };

    public static SeoPayload Seo() => new()
    {
        SiteName = ShopName,
        TitleTemplate = "%s | قصر روشنایی",
        HomeTitle = "فروشگاه قصر روشنایی – لوستر و لوازم روشنایی",
        DefaultDescription = "گالری قصر روشنایی از سال ۱۳۸۵ پیشرو در واردات لوسترهای مدرن و کلاسیک؛ ضمانت واقعی و خدمات پس از فروش.",
        DefaultOgMediaId = null,
    };

    /// <summary>
    /// <paramref name="aboutContentPageId"/>/<paramref name="contactContentPageId"/>: the seeded about/contact content pages'
    /// ids, so the menu links to them as entity references (hidden while they are still draft) instead of a typed "/about"
    /// URL. Left out (the ids are not known yet, e.g. the <see cref="ForKind"/> fallback), the item falls back to the typed URL.
    /// </summary>
    public static ChromePayload Chrome(int? aboutContentPageId = null, int? contactContentPageId = null) => new()
    {
        Announcement = new AnnouncementBar { Enabled = false, Text = string.Empty, Link = null, Style = "primary", Dismissible = true },
        HeaderMenu =
        [
            new MenuItem { Label = "خانه", Link = "/" },
            new MenuItem { Label = "فروشگاه", Link = "/", AllCategories = true },
            aboutContentPageId is { } aboutId
                ? new MenuItem { Label = "درباره ما", Entity = new LinkEntity { Type = RouteTargetTypes.ContentPage, Id = aboutId } }
                : new MenuItem { Label = "درباره ما", Link = "/about" },
            contactContentPageId is { } contactId
                ? new MenuItem { Label = "تماس", Entity = new LinkEntity { Type = RouteTargetTypes.ContentPage, Id = contactId } }
                : new MenuItem { Label = "تماس", Link = "/contact" },
        ],
        Footer = new FooterContent { About = "گالری قصر روشنایی؛ از سال ۱۳۸۵ پیشرو در واردات لوسترهای مدرن و کلاسیک.", Columns = [] },
        Social = new SocialLinks
        {
            Twitter = "https://x.com/e_ghasr",
            Pinterest = "https://www.pinterest.com/ghasr_e_roushanaei",
            Aparat = "https://www.aparat.com/ghasr_e_roushanaei",
        },
    };

    public static BadgesPayload Badges() => new();

    /// <summary>Hero (text only until the owner uploads images), newest products, the top-level categories (when there are at least two) and the promises.</summary>
    public static HomePayload Home(IReadOnlyList<int> topCategoryIds)
    {
        var blocks = new List<HomeBlock>
        {
            new()
            {
                Id = Guid.NewGuid(), Type = "hero", Enabled = true,
                Data = new HeroData
                {
                    AutoplaySeconds = 0,
                    Slides = [new HeroSlide { Title = "ترکیبی از کلاسیک و مدرن", Text = "شکوه و زیبایی با لوسترها قصر روشنایی", LinkLabel = "مشاهده محصولات", Link = "/search" }],
                },
            },
            new() { Id = Guid.NewGuid(), Type = "productRow", Enabled = true, Data = new ProductRowData { Title = "جدیدترین محصولات", Source = "newest", Limit = 8 } },
        };

        if (topCategoryIds.Count >= 2)
        {
            blocks.Add(new HomeBlock
            {
                Id = Guid.NewGuid(), Type = "categoryTiles", Enabled = true,
                Data = new CategoryTilesData { Items = topCategoryIds.Take(12).Select(id => new CategoryTileItem { CategoryId = id }).ToList() },
            });
        }

        blocks.Add(new HomeBlock
        {
            Id = Guid.NewGuid(), Type = "text", Enabled = true,
            Data = new TextBlockData { Heading = "چرا قصر روشنایی؟", Body = PromisesMarkdown },
        });
        return new HomePayload { Blocks = blocks };
    }

    public static object ForKind(SiteKind kind) => kind switch
    {
        SiteKind.Theme => Theme(),
        SiteKind.Home => Home([]),
        SiteKind.Chrome => Chrome(),
        SiteKind.Seo => Seo(),
        SiteKind.Badges => Badges(),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
