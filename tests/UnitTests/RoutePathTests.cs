using Shop.Domain.Routing;

namespace Shop.UnitTests;

public class RoutePathTests
{
    [Theory]
    [InlineData("/product/led-bulb", "/product/led-bulb")]
    [InlineData("product/led-bulb", "/product/led-bulb")]
    [InlineData("  /Product/LED-Bulb/  ", "/product/led-bulb")]
    [InlineData("/lustre/crystal///", "/lustre/crystal")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("/", "")]
    public void Normalize_GivesTheCanonicalSpelling(string input, string expected) =>
        Assert.Equal(expected, RoutePath.Normalize(input));

    [Fact]
    public void Normalize_LowerCasesOnlyAsciiLetters_SoOtherScriptsStillFailValidation()
    {
        var persian = RoutePath.Normalize("/لوستر");
        Assert.Equal("/لوستر", persian);
        Assert.NotNull(RoutePath.Validate(persian));
        Assert.NotNull(RoutePath.Validate(RoutePath.Normalize("/İstanbul"))); // a non-ASCII capital is not folded into ASCII
    }

    [Theory]
    [InlineData("/about")]
    [InlineData("/category/chandeliers")]
    [InlineData("/lustre/crystal")]
    [InlineData("/a/b/c/d")]
    [InlineData("/led-9w-e27")]
    [InlineData("/product/9")]
    [InlineData("/blog/my-post")]
    [InlineData("/blogger")]
    [InlineData("/cartoons")]
    public void Validate_AcceptsPlainAsciiPaths(string path) => Assert.Null(RoutePath.Validate(path));

    [Theory]
    [InlineData("", "api.validation.route-path-required")]
    [InlineData("/a/b/c/d/e", "api.validation.route-path-invalid")]
    [InlineData("/-a", "api.validation.route-path-invalid")]
    [InlineData("/a-", "api.validation.route-path-invalid")]
    [InlineData("/a--b", "api.validation.route-path-invalid")]
    [InlineData("/a//b", "api.validation.route-path-invalid")]
    [InlineData("/a_b", "api.validation.route-path-invalid")]
    [InlineData("/a.b", "api.validation.route-path-invalid")]
    [InlineData("/a b", "api.validation.route-path-invalid")]
    [InlineData("/a%2Fb", "api.validation.route-path-invalid")]
    [InlineData("/لوستر", "api.validation.route-path-invalid")]
    [InlineData("/cart", "api.validation.route-path-reserved")]
    [InlineData("/admin/users", "api.validation.route-path-reserved")]
    [InlineData("/order/abc", "api.validation.route-path-reserved")]
    [InlineData("/blog", "api.validation.route-path-reserved")]
    public void Validate_RefusesBadAndReservedPaths(string path, string key) =>
        Assert.StartsWith(key, RoutePath.Validate(path) ?? "ok");

    [Fact]
    public void Validate_RefusesAPathLongerThanTheLimit()
    {
        var path = "/" + new string('a', RoutePath.MaxLength);
        Assert.StartsWith("api.validation.route-path-too-long", RoutePath.Validate(path));
        Assert.Null(RoutePath.Validate("/" + new string('a', RoutePath.MaxLength - 1)));
    }

    [Fact]
    public void ReservedPaths_AreMatchedByFirstSegment_ExceptTheBlogIndexWhichIsExact()
    {
        Assert.True(ReservedPaths.IsReserved("/blog"));
        Assert.False(ReservedPaths.IsReserved("/blog/post"));
        Assert.True(ReservedPaths.IsReserved("/search"));
        Assert.True(ReservedPaths.IsReserved("/search/anything"));
        Assert.False(ReservedPaths.IsReserved("/searching"));
        Assert.All(ReservedPaths.Segments, s => Assert.True(ReservedPaths.IsReserved("/" + s)));
    }

    [Theory]
    [InlineData(RouteTargetTypes.Category, "chandeliers", "/category/chandeliers")]
    [InlineData(RouteTargetTypes.Product, "led-bulb", "/product/led-bulb")]
    [InlineData(RouteTargetTypes.BlogPost, "led-vs-halogen", "/blog/led-vs-halogen")]
    [InlineData(RouteTargetTypes.ContentPage, "about", "/about")]
    public void TheDefaultPath_IsTheUrlTheEntityAlwaysHad(string type, string slug, string expected)
    {
        Assert.Equal(expected, RouteTargetTypes.DefaultPath(type, slug));
        Assert.Null(RoutePath.Validate(expected));
    }
}
