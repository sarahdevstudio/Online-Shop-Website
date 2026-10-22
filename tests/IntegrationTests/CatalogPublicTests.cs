using System.Net;

namespace Shop.IntegrationTests;

public sealed class CatalogPublicTests(ApiFixture fixture) : CatalogTestBase(fixture)
{
    /// <summary>
    /// Root category with one subcategory:
    ///   A (brand X, base E27): 9W/3000K 100k stock 5, 9W/6500K 100k stock 0, inactive 20W/2700K
    ///   B (brand X, base E14): 12W/6500K 150k stock 3
    ///   C (brand Y, base E27, in the subcategory): 9W/6500K 80k stock 0, 12W/3000K 120k stock 2
    ///   Draft (9W) and Archived (9W/3000K): never listed.
    /// </summary>
    private sealed record Scenario(string Root, string Child, string BrandX, string BrandY, string A, string B, string C, string Draft, string Archived, AdminVariantBody BVariant);

    private async Task<Scenario> BuildAsync(HttpClient admin)
    {
        var attrs = await AttributesAsync(admin);
        var root = Unique("root");
        var child = Unique("child");
        var rootId = (await CreateCategoryAsync(admin, root)).Id;
        var childId = (await CreateCategoryAsync(admin, child, rootId)).Id;
        var brandX = Unique("bx");
        var brandY = Unique("by");
        var xId = (await CreateBrandAsync(admin, brandX)).Id;
        var yId = (await CreateBrandAsync(admin, brandY)).Id;

        var marker = Guid.NewGuid().ToString("N")[..8];
        var a = await CreateProductAsync(admin, rootId, Unique("a"), xId, name: $"لامپ کم‌مصرف ۹ وات {marker}",
            variants: [(100_000, 5), (100_000, 0)]);
        var inactive = await SendOkAsync<AdminVariantBody>(admin, HttpMethod.Post, $"/api/v1/admin/products/{a.Id}/variants",
            new { sku = a.Slug + "-x", price = 90_000, stock = 9, isActive = false, sortOrder = 9 });
        await SetAttributesAsync(admin, a.Id,
            Opt(attrs, "base", "e27"),
            Opt(attrs, "wattage", "9w", a.Variants[0].Id), Opt(attrs, "cct", "3000k", a.Variants[0].Id),
            Opt(attrs, "wattage", "9w", a.Variants[1].Id), Opt(attrs, "cct", "6500k", a.Variants[1].Id),
            Opt(attrs, "wattage", "20w", inactive.Id), Opt(attrs, "cct", "2700k", inactive.Id));

        var b = await CreateProductAsync(admin, rootId, Unique("b"), xId, variants: [(150_000, 3)]);
        await SetAttributesAsync(admin, b.Id,
            Opt(attrs, "base", "e14"), Opt(attrs, "wattage", "12w", b.Variants[0].Id), Opt(attrs, "cct", "6500k", b.Variants[0].Id));

        var c = await CreateProductAsync(admin, childId, Unique("c"), yId, variants: [(80_000, 0), (120_000, 2)]);
        await SetAttributesAsync(admin, c.Id,
            Opt(attrs, "base", "e27"),
            Opt(attrs, "wattage", "9w", c.Variants[0].Id), Opt(attrs, "cct", "6500k", c.Variants[0].Id),
            Opt(attrs, "wattage", "12w", c.Variants[1].Id), Opt(attrs, "cct", "3000k", c.Variants[1].Id));

        var draft = await CreateProductAsync(admin, rootId, Unique("draft"), xId, status: "draft");
        await SetAttributesAsync(admin, draft.Id, Opt(attrs, "wattage", "9w", draft.Variants[0].Id));
        var archived = await CreateProductAsync(admin, rootId, Unique("archived"), xId, status: "archived", variants: [(70_000, 4)]);
        await SetAttributesAsync(admin, archived.Id,
            Opt(attrs, "wattage", "9w", archived.Variants[0].Id), Opt(attrs, "cct", "3000k", archived.Variants[0].Id));

        return new Scenario(root, child, brandX, brandY, a.Slug, b.Slug, c.Slug, draft.Slug, archived.Slug, b.Variants[0]);
    }

    private Task<ProductListBody> ListAsync(string query) =>
        GetOkAsync<ProductListBody>(NewClient(), "/api/v1/catalog/products?" + query);

    private async Task<Scenario> SetUpAsync() => await BuildAsync(await LoginAdminAsync());

    private static string[] Slugs(ProductListBody list) => list.Items.Select(i => i.Slug).Order().ToArray();

    private static string[] Expected(params string[] slugs) => slugs.Order().ToArray();

    [Fact]
    public async Task Category_ListsPublishedProducts_IncludingSubcategories_NeverDraftOrArchived()
    {
        var s = await SetUpAsync();

        var root = await ListAsync($"category={s.Root}");
        var child = await ListAsync($"category={s.Child}");

        Assert.Equal(Expected(s.A, s.B, s.C), Slugs(root));
        Assert.Equal(3, root.Total);
        Assert.Equal(Expected(s.C), Slugs(child));
    }

    [Fact]
    public async Task AttributeFilter_IsOrWithinAnAttribute()
    {
        var s = await SetUpAsync();

        Assert.Equal(Expected(s.A, s.C), Slugs(await ListAsync($"category={s.Root}&wattage=9w")));
        Assert.Equal(Expected(s.A, s.B, s.C), Slugs(await ListAsync($"category={s.Root}&wattage=9w,12w")));
        Assert.Equal(Expected(s.A, s.B, s.C), Slugs(await ListAsync($"category={s.Root}&wattage=9w&wattage=12w")));
    }

    [Fact]
    public async Task AttributeFilter_IsAndAcrossAttributes()
    {
        var s = await SetUpAsync();

        Assert.Equal(Expected(s.A, s.C), Slugs(await ListAsync($"category={s.Root}&wattage=9w&base=e27")));
        Assert.Equal(Expected(s.B), Slugs(await ListAsync($"category={s.Root}&wattage=9w,12w&base=e14")));
        Assert.Empty((await ListAsync($"category={s.Root}&wattage=9w&base=e14")).Items);
        Assert.Equal(Expected(s.C), Slugs(await ListAsync($"category={s.Root}&wattage=12w&base=e27&brand={s.BrandY}")));
    }

    [Fact]
    public async Task AttributeFilters_MatchPerProduct_AsDocumented()
    {
        var s = await SetUpAsync();

        // C has a 12W variant (3000K) and a 6500K variant (9W): per-product matching lists it (docs/06 §3).
        Assert.Equal(Expected(s.B, s.C), Slugs(await ListAsync($"category={s.Root}&wattage=12w&cct=6500k")));
    }

    [Fact]
    public async Task InactiveVariants_DoNotMatchFilters_OrAppearInFacets()
    {
        var s = await SetUpAsync();

        var list = await ListAsync($"category={s.Root}&wattage=20w");

        Assert.Empty(list.Items);
        var wattage = list.Facets.Attributes.Single(f => f.Code == "wattage");
        Assert.DoesNotContain(wattage.Options, o => o.Slug == "20w");
        // Only the inactive variant is 2700K, so the unfiltered category has no 2700K option either.
        var all = await ListAsync($"category={s.Root}");
        Assert.DoesNotContain(all.Facets.Attributes.Single(f => f.Code == "cct").Options, o => o.Slug == "2700k");
    }

    [Fact]
    public async Task UnknownFilterValues_MatchNothing_UnknownKeysAreIgnored()
    {
        var s = await SetUpAsync();

        Assert.Empty((await ListAsync($"category={s.Root}&wattage=999w")).Items);
        Assert.Empty((await ListAsync($"category={s.Root}&brand=no-such-brand")).Items);
        Assert.Equal(3, (await ListAsync($"category={s.Root}&utm_source=x&fbclid=abc")).Total);
    }

    [Fact]
    public async Task Facets_IgnoreTheirOwnSelection_AndRespectAllOtherFilters()
    {
        var s = await SetUpAsync();

        var list = await ListAsync($"category={s.Root}&wattage=9w");

        Assert.Equal(Expected(s.A, s.C), Slugs(list));
        // Own selection ignored: 12W stays selectable with its count over the unfiltered set (B, C).
        var wattage = list.Facets.Attributes.Single(f => f.Code == "wattage").Options.ToDictionary(o => o.Slug, o => o.Count);
        Assert.Equal(new Dictionary<string, int> { ["9w"] = 2, ["12w"] = 2 }, wattage);
        // Other facets are counted over the 9W result (A, C); a product counts once even with two matching variants.
        var cct = list.Facets.Attributes.Single(f => f.Code == "cct").Options.ToDictionary(o => o.Slug, o => o.Count);
        Assert.Equal(new Dictionary<string, int> { ["3000k"] = 2, ["6500k"] = 2 }, cct);
        var bases = list.Facets.Attributes.Single(f => f.Code == "base").Options.ToDictionary(o => o.Slug, o => o.Count);
        Assert.Equal(new Dictionary<string, int> { ["e27"] = 2 }, bases);
        var brands = list.Facets.Brands.ToDictionary(o => o.Slug, o => o.Count);
        Assert.Equal(new Dictionary<string, int> { [s.BrandX] = 1, [s.BrandY] = 1 }, brands);
        Assert.Equal(new PriceRangeBody(80_000, 100_000), list.Facets.Price);
        Assert.Equal("مهتابی (۶۵۰۰K)", list.Facets.Attributes.Single(f => f.Code == "cct").Options.Single(o => o.Slug == "6500k").Value);
    }

    [Fact]
    public async Task BrandFacet_IgnoresTheBrandSelection()
    {
        var s = await SetUpAsync();

        var list = await ListAsync($"category={s.Root}&brand={s.BrandX}");

        Assert.Equal(Expected(s.A, s.B), Slugs(list));
        Assert.Equal(new Dictionary<string, int> { [s.BrandX] = 2, [s.BrandY] = 1 }, list.Facets.Brands.ToDictionary(o => o.Slug, o => o.Count));
    }

    [Fact]
    public async Task PriceFilter_UsesTheCheapestActiveVariant()
    {
        var s = await SetUpAsync();

        var list = await ListAsync($"category={s.Root}&minPrice=90000&maxPrice=130000");

        // A = 100k (its inactive 90k variant doesn't count), B = 150k, C = 80k.
        Assert.Equal(Expected(s.A), Slugs(list));
        Assert.Equal(new PriceRangeBody(80_000, 150_000), list.Facets.Price);
    }

    [Fact]
    public async Task Cards_ShowCheapestActivePrice_AndStock()
    {
        var s = await SetUpAsync();
        var v = s.BVariant;
        await SendOkAsync<AdminVariantBody>(await LoginAdminAsync(), HttpMethod.Put, $"/api/v1/admin/variants/{v.Id}",
            new { v.Sku, v.Price, v.CompareAtPrice, stock = 0, v.IsActive, v.SortOrder, v.RowVersion });

        var all = (await ListAsync($"category={s.Root}")).Items.ToDictionary(i => i.Slug);
        var inStock = await ListAsync($"category={s.Root}&inStock=true");

        Assert.Equal(80_000, all[s.C].Price);
        Assert.True(all[s.C].InStock); // its 12W variant has stock
        Assert.False(all[s.B].InStock);
        Assert.Equal(Expected(s.A, s.C), Slugs(inStock));
    }

    [Fact]
    public async Task Sorting_ByPrice_And_Paging()
    {
        var s = await SetUpAsync();

        Assert.Equal([s.C, s.A, s.B], (await ListAsync($"category={s.Root}&sort=price-asc")).Items.Select(i => i.Slug));
        Assert.Equal([s.B, s.A, s.C], (await ListAsync($"category={s.Root}&sort=price-desc")).Items.Select(i => i.Slug));
        var page2 = await ListAsync($"category={s.Root}&sort=price-asc&pageSize=2&page=2");
        Assert.Equal([s.B], page2.Items.Select(i => i.Slug));
        Assert.Equal(3, page2.Total);
        Assert.Empty((await ListAsync($"category={s.Root}&page=9")).Items);
    }

    [Fact]
    public async Task Search_MatchesNormalizedText_ArabicLettersAndDigits()
    {
        var s = await SetUpAsync();

        // Stored name: "لامپ کم‌مصرف ۹ وات"; query uses Arabic kaf, no ZWNJ and a Latin digit.
        var list = await ListAsync($"category={s.Root}&q={Uri.EscapeDataString("كم مصرف 9")}");

        Assert.Equal(Expected(s.A), Slugs(list));
    }

    [Fact]
    public async Task InvalidQuery_Returns400_UnknownCategoryReturns404()
    {
        var client = NewClient();

        await AssertProblemAsync(await client.GetAsync("/api/v1/catalog/products?sort=cheapest"), 400);
        await AssertProblemAsync(await client.GetAsync("/api/v1/catalog/products?category=no-such-category"), 404, "category_not_found");
    }

    [Fact]
    public async Task ProductDetail_Draft404_ArchivedReachableButUnavailable()
    {
        var s = await SetUpAsync();
        var client = NewClient();

        await AssertProblemAsync(await client.GetAsync($"/api/v1/catalog/products/{s.Draft}"), 404, "product_not_found");
        var archived = await GetOkAsync<ProductDetailBody>(client, $"/api/v1/catalog/products/{s.Archived}");
        Assert.Equal("archived", archived.Status);
        Assert.All(archived.Variants, v => Assert.False(v.InStock));

        var sitemap = await GetOkAsync<SitemapBody>(client, "/api/v1/catalog/sitemap");
        Assert.Contains(sitemap.Products, p => p.Slug == s.A);
        Assert.DoesNotContain(sitemap.Products, p => p.Slug == s.Draft || p.Slug == s.Archived);
        Assert.Contains(sitemap.Categories, c => c.Slug == s.Child);
    }

    [Fact]
    public async Task ProductDetail_ReturnsAxesSpecsAndVariantValues()
    {
        var s = await SetUpAsync();

        var a = await GetOkAsync<ProductDetailBody>(NewClient(), $"/api/v1/catalog/products/{s.A}");

        Assert.Equal("published", a.Status);
        Assert.Equal(2, a.Variants.Length); // inactive variant hidden
        Assert.Equal(["9w"], a.Axes.Single(x => x.Code == "wattage").Options.Select(o => o.Slug));
        Assert.Equal(["3000k", "6500k"], a.Axes.Single(x => x.Code == "cct").Options.Select(o => o.Slug));
        Assert.Equal("E27", a.Specs.Single(x => x.Code == "base").Value);
        Assert.Equal(new Dictionary<string, string> { ["wattage"] = "9w", ["cct"] = "3000k" }, a.Variants[0].Attributes);
        Assert.True(a.Variants[0].InStock);
        Assert.False(a.Variants[1].InStock);
    }

    [Fact]
    public async Task CategoryTree_HidesInactiveCategories_AndTheirChildren()
    {
        var admin = await LoginAdminAsync();
        var root = Unique("hidden-root");
        var child = Unique("hidden-child");
        var rootId = (await CreateCategoryAsync(admin, root, isActive: false)).Id;
        await CreateCategoryAsync(admin, child, rootId);
        var client = NewClient();

        var tree = await GetOkAsync<CategoryTreeBody[]>(client, "/api/v1/catalog/categories");

        Assert.DoesNotContain(tree, c => c.Slug == root);
        await AssertProblemAsync(await client.GetAsync($"/api/v1/catalog/categories/{child}"), 404, "category_not_found");
    }

    public sealed record CategoryTreeBody(string Slug, string Name);
}
