using Shop.Application.Catalog;

namespace Shop.UnitTests;

public class ProductListQueryParserTests
{
    private static ProductListQuery ParseOk(params (string Key, string?[] Values)[] query)
    {
        var result = ProductListQueryParser.Parse(query.Select(q => (q.Key, (IReadOnlyList<string?>)q.Values)));
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static IReadOnlyDictionary<string, string[]> ParseErrors(params (string Key, string?[] Values)[] query)
    {
        var result = ProductListQueryParser.Parse(query.Select(q => (q.Key, (IReadOnlyList<string?>)q.Values)));
        Assert.False(result.IsSuccess);
        return result.Error!.ValidationErrors!;
    }

    [Fact]
    public void Empty_GivesDefaults()
    {
        var query = ParseOk();

        Assert.Equal(1, query.Page);
        Assert.Equal(ProductListQuery.DefaultPageSize, query.PageSize);
        Assert.Equal(ProductSort.Newest, query.Sort);
        Assert.False(query.InStock);
        Assert.Empty(query.Attributes!);
    }

    [Fact]
    public void AttributeValues_CanBeCommaSeparatedOrRepeated()
    {
        var query = ParseOk(("wattage", ["9w,12w", "15W"]), ("cct", ["3000k"]));

        Assert.Equal(["9w", "12w", "15w"], query.Attributes!["wattage"]);
        Assert.Equal(["3000k"], query.Attributes["cct"]);
    }

    [Fact]
    public void ReservedKeys_AreNotAttributes_AndNonCodeKeysAreIgnored()
    {
        var query = ParseOk(("category", ["LED-Bulbs"]), ("brand", ["zar,philips"]), ("sort", ["price-asc"]),
            ("page", ["2"]), ("minPrice", ["1000"]), ("maxPrice", ["90000"]), ("inStock", ["true"]), ("utm_source", ["x"]));

        Assert.Equal("led-bulbs", query.Category);
        Assert.Equal(["zar", "philips"], query.Brands);
        Assert.Equal(ProductSort.PriceAsc, query.Sort);
        Assert.Equal(2, query.Page);
        Assert.Equal(1000, query.MinPrice);
        Assert.Equal(90000, query.MaxPrice);
        Assert.True(query.InStock);
        Assert.Empty(query.Attributes!);
    }

    [Theory]
    [InlineData("page", "0")]
    [InlineData("page", "abc")]
    [InlineData("pageSize", "1000")]
    [InlineData("sort", "cheapest")]
    [InlineData("inStock", "yes")]
    [InlineData("minPrice", "-5")]
    [InlineData("maxPrice", "1.5")]
    public void InvalidValues_AreValidationErrors(string key, string value)
    {
        var errors = ParseErrors((key, [value]));

        Assert.Contains(key, errors.Keys);
    }

    [Fact]
    public void MinPriceAboveMaxPrice_IsAnError()
    {
        Assert.Contains("minPrice", ParseErrors(("minPrice", ["500"]), ("maxPrice", ["100"])).Keys);
    }

    [Fact]
    public void TooLongQ_IsAnError()
    {
        Assert.Contains("q", ParseErrors(("q", [new string('a', ProductListQuery.QMaxLength + 1)])).Keys);
    }
}
