using Shop.Domain.Security;
using Shop.Domain.Site;

namespace Shop.UnitTests;

public class SitePayloadTests
{
    [Fact]
    public void SiteManage_IsAnAdminPermission()
    {
        Assert.Contains(Permissions.SiteManage, Permissions.All);
        Assert.Contains(Permissions.SiteManage, Roles.PermissionsFor(Roles.Admin));
        Assert.Empty(Roles.PermissionsFor(Roles.Customer));
    }

    [Theory]
    [InlineData("theme", SiteKind.Theme)]
    [InlineData("home", SiteKind.Home)]
    [InlineData("chrome", SiteKind.Chrome)]
    [InlineData("seo", SiteKind.Seo)]
    [InlineData("badges", SiteKind.Badges)]
    public void KindRouteValues_RoundTrip(string route, SiteKind kind)
    {
        Assert.True(SiteNames.TryParseKind(route, out var parsed));
        Assert.Equal(kind, parsed);
        Assert.Equal(route, kind.ToRoute());
    }

    [Theory]
    [InlineData("Theme")]
    [InlineData("")]
    [InlineData("settings")]
    public void UnknownKinds_AreRejected(string route) => Assert.False(SiteNames.TryParseKind(route, out _));

    [Fact]
    public void HomePayload_RoundTripsTypedBlocks_AndKeepsUnknownTypesWithoutData()
    {
        var json = """
            {"blocks":[
              {"id":"3f2b1a0e-9c1d-4a53-8f10-1d9a2b3c4d5e","type":"text","enabled":false,"data":{"heading":"H","body":"B"}},
              {"id":"11111111-1111-1111-1111-111111111111","type":"productRow","enabled":true,"data":{"title":"T","source":"newest","productIds":[],"limit":8}},
              {"id":"22222222-2222-2222-2222-222222222222","type":"fancy","enabled":true,"data":{"x":1}}]}
            """;

        var home = Assert.IsType<HomePayload>(SiteJson.TryDeserialize(SiteKind.Home, json));

        Assert.Equal(3, home.Blocks.Count);
        Assert.False(home.Blocks[0].Enabled);
        Assert.Equal("H", Assert.IsType<TextBlockData>(home.Blocks[0].Data).Heading);
        Assert.Equal(8, Assert.IsType<ProductRowData>(home.Blocks[1].Data).Limit);
        Assert.Null(home.Blocks[2].Data);

        var again = Assert.IsType<HomePayload>(SiteJson.TryDeserialize(SiteKind.Home, SiteJson.Serialize(home)));
        Assert.Equal("B", Assert.IsType<TextBlockData>(again.Blocks[0].Data).Body);
    }

    [Fact]
    public void Payloads_WithWrongShapes_AreNotDeserialized()
    {
        Assert.Null(SiteJson.TryDeserialize(SiteKind.Theme, """{"primary":123}"""));
        Assert.Null(SiteJson.TryDeserialize(SiteKind.Home, """{"blocks":"nope"}"""));
        Assert.Null(SiteJson.TryDeserialize(SiteKind.Seo, "[]"));
    }

    [Fact]
    public void MissingProperties_FallBackToSafeDefaults()
    {
        var theme = Assert.IsType<ThemePayload>(SiteJson.TryDeserialize(SiteKind.Theme, "{}"));
        Assert.Equal(string.Empty, theme.Primary);
        Assert.Null(theme.Logo);
        var chrome = Assert.IsType<ChromePayload>(SiteJson.TryDeserialize(SiteKind.Chrome, "{}"));
        Assert.Empty(chrome.HeaderMenu);
        Assert.False(chrome.Announcement.Enabled);
    }

    [Fact]
    public void SamandehiBadge_HasTwoPerShopTokens()
    {
        var badges = Assert.IsType<BadgesPayload>(SiteJson.TryDeserialize(SiteKind.Badges,
            """{"enamad":{"id":"123","code":"ABCDEFGHIJ"},"samandehi":{"id":"9","verifyCode":"v1234567890","logoCode":"l1234567890"}}"""));
        Assert.Equal("ABCDEFGHIJ", badges.Enamad!.Code);
        Assert.Equal("v1234567890", badges.Samandehi!.VerifyCode);
        Assert.Equal("l1234567890", badges.Samandehi.LogoCode);
    }

    [Fact]
    public void Version_LifecycleIsEnforced()
    {
        var draft = SiteConfigVersion.NewDraft(SiteKind.Seo, "{}", null, DateTime.UtcNow);
        Assert.Null(draft.Version);
        draft.Publish(1, null, DateTime.UtcNow);
        Assert.Equal(SiteState.Published, draft.State);
        Assert.Throws<InvalidOperationException>(() => draft.SetPayload("{}", null, DateTime.UtcNow));
        draft.Archive();
        Assert.Equal(SiteState.Archived, draft.State);
        Assert.Throws<InvalidOperationException>(() => draft.Archive());
    }
}
