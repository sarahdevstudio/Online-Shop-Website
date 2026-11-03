using Shop.Application.Site;
using Shop.Domain.Site;

namespace Shop.UnitTests;

public class SiteLinkTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/about")]
    [InlineData("/category/led-bulbs?sort=newest&page=2#top")]
    [InlineData("https://ghr-co.com")]
    [InlineData("https://www.aparat.com/x_y")]
    public void ValidLinks(string link) => Assert.True(SiteLinks.IsValid(link));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("javascript:alert(1)")]
    [InlineData("JaVaScRiPt:alert(1)")]
    [InlineData("data:text/html,<script>")]
    [InlineData("http://example.com")]
    [InlineData("//evil.com")]
    [InlineData("/\\evil.com")]
    [InlineData("\\evil.com")]
    [InlineData("mailto:a@b.ir")]
    [InlineData("/a b")]
    [InlineData("/<script>")]
    [InlineData("about")]
    [InlineData("https://user:pw@example.com")]
    [InlineData("https://")]
    public void InvalidLinks(string? link) => Assert.False(SiteLinks.IsValid(link));

    [Fact]
    public void TooLongLink_IsRefused() => Assert.False(SiteLinks.IsValid("/" + new string('a', 400)));
}

public class SocialLinkTests
{
    [Theory]
    [InlineData("twitter", "e_ghasr", "https://x.com/e_ghasr")]
    [InlineData("twitter", "@e_ghasr", "https://x.com/e_ghasr")]
    [InlineData("twitter", "https://twitter.com/e_ghasr?lang=fa", "https://x.com/e_ghasr")]
    [InlineData("twitter", "https://x.com/e_ghasr", "https://x.com/e_ghasr")]
    [InlineData("pinterest", "ghasr_e_roushanaei", "https://www.pinterest.com/ghasr_e_roushanaei")]
    [InlineData("pinterest", "https://www.pinterest.com/ghasr_e_roushanaei", "https://www.pinterest.com/ghasr_e_roushanaei")]
    [InlineData("aparat", "ghasr_e_roushanaei", "https://www.aparat.com/ghasr_e_roushanaei")]
    [InlineData("aparat", "https://www.aparat.com/ghasr_e_roushanaei", "https://www.aparat.com/ghasr_e_roushanaei")]
    [InlineData("telegram", "@ghasr_shop", "https://t.me/ghasr_shop")]
    [InlineData("telegram", "https://t.me/ghasr_shop", "https://t.me/ghasr_shop")]
    [InlineData("whatsapp", "+989121234567", "https://wa.me/989121234567")]
    [InlineData("whatsapp", "https://wa.me/989121234567", "https://wa.me/989121234567")]
    [InlineData("youtube", "@ghasr", "https://www.youtube.com/@ghasr")]
    [InlineData("youtube", "https://www.youtube.com/channel/UCabc_def-1", "https://www.youtube.com/channel/UCabc_def-1")]
    [InlineData("linkedin", "https://www.linkedin.com/company/ghasr", "https://www.linkedin.com/company/ghasr")]
    [InlineData("linkedin", "company/ghasr", "https://www.linkedin.com/company/ghasr")]
    public void Valid_IsNormalizedToHttps(string platform, string input, string expected) =>
        Assert.Equal(expected, SocialLinkNormalizer.Normalize(platform, input));

    [Theory]
    [InlineData("twitter", "https://evil.com/e_ghasr")]
    [InlineData("twitter", "http://twitter.com/e_ghasr")]
    [InlineData("twitter", "javascript:alert(1)")]
    [InlineData("twitter", "https://twitter.com/a/b")]
    [InlineData("twitter", "not a handle!")]
    [InlineData("telegram", "ab")]
    [InlineData("whatsapp", "09ab")]
    [InlineData("pinterest", "https://t.me/x_y_z")]
    [InlineData("youtube", "https://youtube.com.evil.com/@x")]
    [InlineData("linkedin", "ghasr")]
    [InlineData("instagram", "parsagallery2020")]
    [InlineData("twitter", "")]
    public void Invalid_IsRejected(string platform, string input) => Assert.Null(SocialLinkNormalizer.Normalize(platform, input));
}

public class SitePayloadValidatorTests
{
    private static SiteReferences Refs(params (int Id, string Purpose)[] media) => new(
        media.ToDictionary(m => m.Id, m => m.Purpose), new HashSet<int> { 1, 2, 3 }, new HashSet<int> { 10, 11, 12 });

    private static ThemePayload Theme() => new()
    {
        Primary = "#5B0057", Secondary = "#76007c", Accent = "#d8c831", Background = "#ffffff", Surface = "#f2f2f2", Text = "#1a1a1a",
        Success = "#1b7f3b", Warning = "#8a5a00", Danger = "#b3261e", Font = "vazirmatn", Radius = "sm",
    };

    private static SiteValidationResult Validate(SiteKind kind, object payload, SiteReferences? refs = null) =>
        SitePayloadValidator.Validate(kind, payload, refs ?? Refs());

    [Fact]
    public void Theme_Valid_NormalizesColorsToLowerCase()
    {
        var result = Validate(SiteKind.Theme, Theme());

        Assert.True(result.IsValid);
        Assert.Equal("#5b0057", ((ThemePayload)result.Payload).Primary);
    }

    [Theory]
    [InlineData("primary", "red")]
    [InlineData("primary", "rgb(1,2,3)")]
    [InlineData("secondary", "#12345")]
    [InlineData("danger", "")]
    public void Theme_BadColors_AreErrorsOnThatField(string field, string value)
    {
        var theme = field switch
        {
            "primary" => Theme() with { Primary = value },
            "secondary" => Theme() with { Secondary = value },
            _ => Theme() with { Danger = value },
        };

        Assert.Contains(field, Validate(SiteKind.Theme, theme).Errors.Keys);
    }

    [Theory]
    [InlineData("IRANSans")]
    [InlineData("samim")]
    [InlineData("")]
    public void Theme_FontOutsideTheAllowList_IsRefused(string font) =>
        Assert.Contains("font", Validate(SiteKind.Theme, Theme() with { Font = font }).Errors.Keys);

    [Theory]
    [InlineData("vazirmatn")]
    [InlineData("Estedad")]
    [InlineData("shabnam")]
    [InlineData("sahel")]
    public void Theme_AllowListedFonts_AreAccepted(string font) => Assert.True(Validate(SiteKind.Theme, Theme() with { Font = font }).IsValid);

    [Fact]
    public void Theme_Logo_NeedsAnExistingLogoImageAndAlt()
    {
        var ok = Validate(SiteKind.Theme, Theme() with { Logo = new ThemeLogo { MediaId = 5, Alt = " قصر   روشنایی " } }, Refs((5, "logo")));
        Assert.True(ok.IsValid);
        Assert.Equal("قصر روشنایی", ((ThemePayload)ok.Payload).Logo!.Alt);

        Assert.Contains("logo.mediaId", Validate(SiteKind.Theme, Theme() with { Logo = new ThemeLogo { MediaId = 6, Alt = "x" } }, Refs((5, "logo"))).Errors.Keys);
        Assert.Contains("logo.mediaId", Validate(SiteKind.Theme, Theme() with { Logo = new ThemeLogo { MediaId = 5, Alt = "x" } }, Refs((5, "hero"))).Errors.Keys);
        Assert.Contains("logo.alt", Validate(SiteKind.Theme, Theme() with { Logo = new ThemeLogo { MediaId = 5, Alt = " " } }, Refs((5, "logo"))).Errors.Keys);
    }

    private static HomeBlock Block(object data, string type, bool enabled = true) => new() { Id = Guid.NewGuid(), Type = type, Enabled = enabled, Data = data };

    private static HomePayload Home(params HomeBlock[] blocks) => new() { Blocks = [.. blocks] };

    [Fact]
    public void Home_Valid_AllBlockTypes()
    {
        var home = Home(
            Block(new HeroData { AutoplaySeconds = 6, Slides = [new HeroSlide { MediaId = 7, Alt = "نمای لوستر", Title = "ترکیبی از کلاسیک و مدرن", LinkLabel = "مشاهده", Link = "/search" }] }, "hero"),
            Block(new CategoryTilesData { Items = [new CategoryTileItem { CategoryId = 1 }, new CategoryTileItem { CategoryId = 2, Label = "لوستر" }] }, "categoryTiles"),
            Block(new ProductRowData { Title = "جدیدترین", Source = "newest", Limit = 8 }, "productRow"),
            Block(new ProductRowData { Title = "تخفیف‌ها", Source = "discounted", Limit = 4 }, "productRow"),
            Block(new ProductRowData { Title = "دسته", Source = "category", CategoryId = 2, Limit = 12 }, "productRow"),
            Block(new ProductRowData { Title = "منتخب", Source = "handpicked", ProductIds = [10, 11], Limit = 6 }, "productRow"),
            Block(new TextBlockData { Heading = "وعده‌ها", Body = "**ضمانت**" }, "text"),
            Block(new BannerData { MediaId = 8, Alt = "بنر", Link = "https://ghr-co.com", Height = "md" }, "banner"));

        var result = Validate(SiteKind.Home, home, Refs((7, "hero"), (8, "banner")));

        Assert.True(result.IsValid, string.Join(";", result.Errors.Keys));
        Assert.Equal(8, ((HomePayload)result.Payload).Blocks.Count);
    }

    [Fact]
    public void Home_DropsUnknownBlockTypes_AndGivesNewIds()
    {
        var home = Home(
            new HomeBlock { Type = "carousel3d", Data = null },
            new HomeBlock { Id = Guid.Empty, Type = "text", Enabled = true, Data = new TextBlockData { Body = "متن" } });

        var result = (HomePayload)Validate(SiteKind.Home, home).Payload;

        Assert.Single(result.Blocks);
        Assert.NotEqual(Guid.Empty, result.Blocks[0].Id);
    }

    [Fact]
    public void Home_MoreThan20Blocks_IsRefused()
    {
        var blocks = Enumerable.Range(0, 21).Select(_ => Block(new TextBlockData { Body = "x" }, "text")).ToArray();

        Assert.Contains("blocks", Validate(SiteKind.Home, Home(blocks)).Errors.Keys);
        Assert.True(Validate(SiteKind.Home, Home(blocks.Take(20).ToArray())).IsValid);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("//evil.com")]
    [InlineData("http://example.com")]
    public void Home_BadLinks_AreErrorsAtTheirPath(string link)
    {
        var home = Home(Block(new HeroData { Slides = [new HeroSlide { Title = "t", LinkLabel = "l", Link = link }] }, "hero"));

        Assert.Contains("blocks[0].data.slides[0].link", Validate(SiteKind.Home, home).Errors.Keys);
    }

    [Fact]
    public void Home_ReferencesMustExist()
    {
        var home = Home(
            Block(new BannerData { MediaId = 99, Alt = "x", Height = "sm" }, "banner"),
            Block(new CategoryTilesData { Items = [new CategoryTileItem { CategoryId = 1 }, new CategoryTileItem { CategoryId = 777 }] }, "categoryTiles"),
            Block(new ProductRowData { Title = "t", Source = "handpicked", ProductIds = [10, 999], Limit = 4 }, "productRow"));

        var errors = Validate(SiteKind.Home, home).Errors.Keys;

        Assert.Contains("blocks[0].data.mediaId", errors);
        Assert.Contains("blocks[1].data.items[1].categoryId", errors);
        Assert.Contains("blocks[2].data.productIds", errors);
    }

    [Fact]
    public void Home_BoundsOfEachBlock()
    {
        var home = Home(
            Block(new HeroData { AutoplaySeconds = 3, Slides = [] }, "hero"),
            Block(new CategoryTilesData { Items = [new CategoryTileItem { CategoryId = 1 }] }, "categoryTiles"),
            Block(new ProductRowData { Title = "t", Source = "newest", Limit = 3 }, "productRow"),
            Block(new TextBlockData { Heading = new string('ا', 81), Body = new string('x', 3001) }, "text"),
            Block(new BannerData { MediaId = 0, Alt = "", Height = "xl" }, "banner"));

        var errors = Validate(SiteKind.Home, home).Errors.Keys;

        Assert.Contains("blocks[0].data.autoplaySeconds", errors);
        Assert.Contains("blocks[0].data.slides", errors);
        Assert.Contains("blocks[1].data.items", errors);
        Assert.Contains("blocks[2].data.limit", errors);
        Assert.Contains("blocks[3].data.heading", errors);
        Assert.Contains("blocks[3].data.body", errors);
        Assert.Contains("blocks[4].data.height", errors);
        Assert.Contains("blocks[4].data.alt", errors);
    }

    [Fact]
    public void Home_ProductRowSource_ClearsIrrelevantFields()
    {
        var home = Home(Block(new ProductRowData { Title = "t", Source = "newest", CategoryId = 2, ProductIds = [10], Limit = 8 }, "productRow"));

        var row = (ProductRowData)((HomePayload)Validate(SiteKind.Home, home).Payload).Blocks[0].Data!;

        Assert.Null(row.CategoryId);
        Assert.Empty(row.ProductIds);
    }

    [Fact]
    public void Chrome_Valid_NormalizesSocialLinks()
    {
        var chrome = new ChromePayload
        {
            Announcement = new AnnouncementBar { Enabled = true, Text = "ارسال رایگان", Link = "/search", Style = "accent" },
            HeaderMenu = [new MenuItem { Label = "خانه", Link = "/" }, new MenuItem { Label = "فروشگاه", Link = "/", AllCategories = true }],
            Footer = new FooterContent { About = "درباره", Columns = [new FooterColumn { Title = "راهنما", Links = [new FooterLink { Label = "تماس", Link = "/contact" }] }] },
            Social = new SocialLinks { Twitter = "e_ghasr", Pinterest = "https://www.pinterest.com/ghasr_e_roushanaei" },
        };

        var result = Validate(SiteKind.Chrome, chrome);

        Assert.True(result.IsValid, string.Join(";", result.Errors.Keys));
        var normalized = (ChromePayload)result.Payload;
        Assert.Equal("https://x.com/e_ghasr", normalized.Social.Twitter);
        Assert.Null(normalized.Social.Aparat);
    }

    [Fact]
    public void Chrome_Bounds_AndBadValues()
    {
        var chrome = new ChromePayload
        {
            Announcement = new AnnouncementBar { Enabled = true, Text = "", Link = "javascript:x", Style = "neon" },
            HeaderMenu = Enumerable.Range(0, 9).Select(i => new MenuItem { Label = "m" + i, Link = "/" }).ToList(),
            Footer = new FooterContent
            {
                About = new string('ا', 301),
                Columns = Enumerable.Range(0, 5).Select(i => new FooterColumn { Title = "c", Links = [] }).ToList(),
            },
            Social = new SocialLinks { Twitter = "https://evil.com/x" },
        };

        var errors = Validate(SiteKind.Chrome, chrome).Errors.Keys;

        Assert.Contains("announcement.text", errors);
        Assert.Contains("announcement.link", errors);
        Assert.Contains("announcement.style", errors);
        Assert.Contains("headerMenu", errors);
        Assert.Contains("footer.about", errors);
        Assert.Contains("footer.columns", errors);
        Assert.Contains("social.twitter", errors);
    }

    [Fact]
    public void Chrome_AllCategoriesItem_IgnoresItsChildren_AndNeedsNoLink()
    {
        var chrome = new ChromePayload
        {
            Announcement = new AnnouncementBar { Style = "primary" },
            HeaderMenu = [new MenuItem { Label = "فروشگاه", AllCategories = true, Children = [new MenuChild { Label = "x", Link = "javascript:x" }] }],
        };

        var result = Validate(SiteKind.Chrome, chrome);

        Assert.True(result.IsValid);
        Assert.Empty(((ChromePayload)result.Payload).HeaderMenu[0].Children);
    }

    [Theory]
    [InlineData("%s | قصر روشنایی", true)]
    [InlineData("قصر روشنایی - %s", true)]
    [InlineData("بدون جانشین", false)]
    [InlineData("%s و %s", false)]
    public void Seo_TitleTemplate_NeedsExactlyOnePlaceholder(string template, bool ok)
    {
        var seo = new SeoPayload { SiteName = "قصر روشنایی", TitleTemplate = template, HomeTitle = "خانه", DefaultDescription = "توضیح" };

        Assert.Equal(ok, Validate(SiteKind.Seo, seo).IsValid);
    }

    [Fact]
    public void Seo_Bounds()
    {
        var seo = new SeoPayload { SiteName = "ق", TitleTemplate = "%s" + new string('ا', 70), HomeTitle = new string('ا', 71), DefaultDescription = new string('ا', 161), DefaultOgMediaId = 99 };

        var errors = Validate(SiteKind.Seo, seo).Errors.Keys;

        Assert.Contains("siteName", errors);
        Assert.Contains("titleTemplate", errors);
        Assert.Contains("homeTitle", errors);
        Assert.Contains("defaultDescription", errors);
        Assert.Contains("defaultOgMediaId", errors);
    }

    [Fact]
    public void Badges_AreIdentifiersOnly()
    {
        var ok = new BadgesPayload
        {
            Enamad = new EnamadBadge { Id = "123456", Code = "AbCdEfGhIjKlMnOp" },
            Samandehi = new SamandehiBadge { Id = "98765", VerifyCode = "abcdefghij12", LogoCode = "klmnopqrst34" },
        };
        Assert.True(Validate(SiteKind.Badges, ok).IsValid);

        var bad = new BadgesPayload
        {
            Enamad = new EnamadBadge { Id = "12ab", Code = "<script>alert(1)</script>" },
            Samandehi = new SamandehiBadge { Id = "1", VerifyCode = "short", LogoCode = "x y z x y z x y z" },
        };
        var errors = Validate(SiteKind.Badges, bad).Errors.Keys;
        Assert.Contains("enamad.id", errors);
        Assert.Contains("enamad.code", errors);
        Assert.Contains("samandehi.verifyCode", errors);
        Assert.Contains("samandehi.logoCode", errors);
    }

    [Fact]
    public void Badges_EmptyObjects_BecomeNull()
    {
        var result = Validate(SiteKind.Badges, new BadgesPayload { Enamad = new EnamadBadge(), Samandehi = new SamandehiBadge() });

        Assert.True(result.IsValid);
        Assert.Null(((BadgesPayload)result.Payload).Enamad);
        Assert.Null(((BadgesPayload)result.Payload).Samandehi);
    }

    [Fact]
    public void CollectReferences_ListsEveryReferencedId()
    {
        var home = Home(
            Block(new HeroData { Slides = [new HeroSlide { MediaId = 4 }, new HeroSlide()] }, "hero"),
            Block(new CategoryTilesData { Items = [new CategoryTileItem { CategoryId = 1 }] }, "categoryTiles"),
            Block(new ProductRowData { Source = "category", CategoryId = 3, ProductIds = [10] }, "productRow"));

        var ids = SitePayloadValidator.CollectReferences(home);

        Assert.Equal([4], ids.MediaIds);
        Assert.Equal([1, 3], ids.CategoryIds.Order());
        Assert.Equal([10], ids.ProductIds);
    }
}
