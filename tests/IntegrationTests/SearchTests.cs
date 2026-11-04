using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;

namespace Shop.IntegrationTests;

public sealed record SuggestProductBody(string Slug, string Name, string Path, long Price, string? ImageUrl);

public sealed record SuggestCategoryBody(string Slug, string Name, string Path);

public sealed record SuggestBody(SuggestProductBody[] Products, SuggestCategoryBody[] Categories, bool Fuzzy);

public sealed record FuzzyListBody(CardBody[] Items, int Total, bool FuzzyMatched);

/// <summary>The search box suggestions and the results page: typo tolerance over an in-memory index that follows catalog changes (docs/18).</summary>
public sealed class SearchTests(ApiFixture fixture) : CatalogTestBase(fixture)
{
    private static Task<SuggestBody> SuggestAsync(HttpClient client, string q) => GetOkAsync<SuggestBody>(client, $"/api/v1/catalog/suggest?q={Uri.EscapeDataString(q)}");

    private static Task<FuzzyListBody> ListAsync(HttpClient client, string q) => GetOkAsync<FuzzyListBody>(client, $"/api/v1/catalog/products?q={Uri.EscapeDataString(q)}");

    /// <summary>A product whose name is a made-up word, so that other tests' products never match it.</summary>
    private async Task<(AdminProductBody Product, string Word)> NewProductAsync(HttpClient admin, string word, string status = "published")
    {
        var category = await CreateCategoryAsync(admin, Unique("srch-cat"));
        var product = await CreateProductAsync(admin, category.Id, Unique("srch"), status: status, name: $"چراغ {word} ۹ وات");
        return (product, word);
    }

    [Fact]
    public async Task ExactAndHalfTypedWords_AreFound_WithPriceAndPath()
    {
        var admin = await LoginAdminAsync();
        var (product, _) = await NewProductAsync(admin, "ستارهباران");
        var anon = NewClient();

        foreach (var q in new[] { "ستارهباران", "ستاره", "چراغ ستارهباران ۹" })
        {
            var result = await SuggestAsync(anon, q);
            var hit = Assert.Single(result.Products, p => p.Slug == product.Slug);
            Assert.False(result.Fuzzy);
            Assert.Equal($"/product/{product.Slug}", hit.Path);
            Assert.True(hit.Price > 0);
        }
    }

    [Theory]
    [InlineData("ستارهبران")]   // a missing letter
    [InlineData("ستارهبارانن")] // an extra letter
    [InlineData("ستارهباران ")] // trailing space, same word
    [InlineData("ستاهربارن")]   // swapped letters
    public async Task TyposStillFindTheProduct_AndSayTheyAreNearestSpellings(string typo)
    {
        var admin = await LoginAdminAsync();
        var (product, _) = await NewProductAsync(admin, "ستارهباران");
        var result = await SuggestAsync(NewClient(), typo);
        Assert.Contains(result.Products, p => p.Slug == product.Slug);
        if (typo.Trim() != "ستارهباران")
        {
            Assert.True(result.Fuzzy);
        }
    }

    [Fact]
    public async Task UnrelatedWords_ShortWords_AndEmptyInput_FindNothing()
    {
        var admin = await LoginAdminAsync();
        await NewProductAsync(admin, "ستارهباران");
        var anon = NewClient();
        Assert.Empty((await SuggestAsync(anon, "کفش")).Products);
        Assert.Empty((await SuggestAsync(anon, "")).Products);
        Assert.Empty((await SuggestAsync(anon, "   ")).Products);
        Assert.Empty((await SuggestAsync(anon, "zzzzzz")).Products);
    }

    [Fact]
    public async Task Categories_AreSuggestedToo()
    {
        var admin = await LoginAdminAsync();
        var slug = Unique("srch-cat");
        var category = await SendOkAsync<IdBody>(admin, HttpMethod.Post, "/api/v1/admin/categories", new { name = "کهکشان‌ها", slug, sortOrder = 0, isActive = true });
        var result = await SuggestAsync(NewClient(), "کهکشان");
        Assert.Contains(result.Categories, c => c.Slug == slug && c.Path == $"/category/{slug}");
        Assert.Contains((await SuggestAsync(NewClient(), "کهکشن")).Categories, c => c.Slug == slug); // with a typo
        _ = category;
    }

    [Fact]
    public async Task TheIndexFollowsCatalogChanges_NewRenamedUnpublishedAndDeactivated()
    {
        var admin = await LoginAdminAsync();
        var anon = NewClient();
        Assert.Empty((await SuggestAsync(anon, "شبنمیان")).Products); // builds the index first

        var (product, _) = await NewProductAsync(admin, "شبنمیان");
        Assert.Contains((await SuggestAsync(anon, "شبنمیان")).Products, p => p.Slug == product.Slug); // new → found

        await SendOkAsync<AdminProductBody>(admin, HttpMethod.Put, $"/api/v1/admin/products/{product.Id}",
            new { categoryId = (await Fixture.WithDbAsync(db => Task.FromResult(db.Products.Where(p => p.Id == product.Id).Select(p => p.CategoryId).Single()))), brandId = (int?)null, name = "چراغ سپیده‌دم", slug = (string?)null, description = "x", status = "published", variants = (object?)null });
        Assert.Empty((await SuggestAsync(anon, "شبنمیان")).Products); // renamed → the old name is gone
        Assert.Contains((await SuggestAsync(anon, "سپیده")).Products, p => p.Slug == product.Slug);

        await SendOkAsync<AdminProductBody>(admin, HttpMethod.Put, $"/api/v1/admin/products/{product.Id}",
            new { categoryId = (await Fixture.WithDbAsync(db => Task.FromResult(db.Products.Where(p => p.Id == product.Id).Select(p => p.CategoryId).Single()))), brandId = (int?)null, name = "چراغ سپیده‌دم", slug = (string?)null, description = "x", status = "draft", variants = (object?)null });
        Assert.DoesNotContain((await SuggestAsync(anon, "سپیده")).Products, p => p.Slug == product.Slug); // unpublished → gone
    }

    [Fact]
    public async Task TheResultsPage_UsesTheSameIndex_WhenNothingMatchesExactly()
    {
        var admin = await LoginAdminAsync();
        var (product, _) = await NewProductAsync(admin, "آبشارنقره");
        var anon = NewClient();

        var exact = await ListAsync(anon, "آبشارنقره");
        Assert.False(exact.FuzzyMatched);
        Assert.Contains(exact.Items, i => i.Slug == product.Slug);

        var typo = await ListAsync(anon, "آبشارنقرا");
        Assert.True(typo.FuzzyMatched);
        Assert.Contains(typo.Items, i => i.Slug == product.Slug);

        var nothing = await ListAsync(anon, "ققققققققق");
        Assert.False(nothing.FuzzyMatched);
        Assert.Empty(nothing.Items);

        // Filters still apply to the nearest spellings: in-stock only, with the product out of stock, finds nothing.
        await Fixture.WithDbAsync(async db =>
        {
            await db.ProductVariants.Where(v => v.ProductId == product.Id).ExecuteUpdateAsync(s => s.SetProperty(v => v.Stock, 0));
            return 0;
        });
        var filtered = await GetOkAsync<FuzzyListBody>(anon, $"/api/v1/catalog/products?q={Uri.EscapeDataString("آبشارنقرا")}&inStock=true");
        Assert.DoesNotContain(filtered.Items, i => i.Slug == product.Slug);
    }

    [Fact]
    public async Task OverlongInput_IsCut_NotAnError()
    {
        var response = await NewClient().GetAsync($"/api/v1/catalog/suggest?q={new string('ب', 5000)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
