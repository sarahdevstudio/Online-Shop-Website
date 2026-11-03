using Shop.Application.Site;
using Shop.Domain.Site;
using Shop.Infrastructure.Site;

namespace Shop.UnitTests;

public class SiteSeedTests
{
    private static SiteReferences Refs() => new(
        new Dictionary<int, string> { [1] = "logo", [2] = "favicon" }, new HashSet<int> { 1, 2, 3 }, new HashSet<int>());

    [Fact]
    public void EveryDefault_IsValid_AndTheThemeMeetsAA()
    {
        var results = new (SiteKind Kind, object Payload)[]
        {
            (SiteKind.Theme, SiteDefaults.Theme(1, 2)),
            (SiteKind.Home, SiteDefaults.Home([1, 2, 3])),
            (SiteKind.Home, SiteDefaults.Home([])),
            (SiteKind.Chrome, SiteDefaults.Chrome()),
            (SiteKind.Seo, SiteDefaults.Seo()),
            (SiteKind.Badges, SiteDefaults.Badges()),
        };

        foreach (var (kind, payload) in results)
        {
            var validated = SitePayloadValidator.Validate(kind, payload, Refs());
            Assert.True(validated.IsValid, $"{kind}: {string.Join(", ", validated.Errors.Keys)}");
        }

        Assert.All(Contrast.Check(SiteDefaults.Theme()), r => Assert.True(r.Ok, r.Message));
    }

    [Fact]
    public void Home_TilesAreOmitted_WhenThereAreFewerThanTwoCategories()
    {
        Assert.DoesNotContain(SiteDefaults.Home([5]).Blocks, b => b.Type == "categoryTiles");
        Assert.Contains(SiteDefaults.Home([5, 6]).Blocks, b => b.Type == "categoryTiles");
    }

    [Fact]
    public void Defaults_RoundTripThroughTheJsonCodec()
    {
        foreach (var kind in Enum.GetValues<SiteKind>())
        {
            var json = SiteJson.Serialize(SiteDefaults.ForKind(kind));
            Assert.NotNull(SiteJson.TryDeserialize(kind, json));
        }
    }

    [Fact]
    public void Chrome_DefaultMenu_HasNoBlog_AndSocialHasNoInstagram()
    {
        var chrome = SiteDefaults.Chrome();

        Assert.DoesNotContain(chrome.HeaderMenu, m => m.Link == "/blog");
        Assert.Equal(["/", "/", "/about", "/contact"], chrome.HeaderMenu.Select(m => m.Link));
        Assert.True(chrome.HeaderMenu[1].AllCategories);
        Assert.Equal("https://x.com/e_ghasr", chrome.Social.Twitter);
        Assert.Null(chrome.Social.Telegram);
    }

    [Theory]
    [InlineData("logo.png", 300, 70)]
    [InlineData("favicon-512.png", 512, 512)]
    public void EmbeddedAssets_AreRealPngs(string name, int width, int height)
    {
        var assembly = typeof(SiteSeeder).Assembly;
        var resource = Assert.Single(assembly.GetManifestResourceNames(), n => n.EndsWith("." + name, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        var info = ImageProbe.Probe(buffer.ToArray());

        Assert.Equal(new ImageInfo("image/png", ".png", width, height), info);
    }
}
