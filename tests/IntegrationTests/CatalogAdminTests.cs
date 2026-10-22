using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Shop.Infrastructure.Catalog;

namespace Shop.IntegrationTests;

public sealed class CatalogAdminTests(ApiFixture fixture) : CatalogTestBase(fixture)
{
    [Fact]
    public async Task AdminCatalog_RequiresCatalogPermission()
    {
        var (customer, _, _) = await RegisterCustomerAsync();

        await AssertProblemAsync(await NewClient().GetAsync("/api/v1/admin/categories"), 401);
        await AssertProblemAsync(await customer.GetAsync("/api/v1/admin/categories"), 403);
        await AssertProblemAsync(await SendAsync(customer, HttpMethod.Post, "/api/v1/admin/brands",
            new { name = "برند", slug = Unique("b"), isActive = true }), 403);
    }

    [Fact]
    public async Task Categories_AreAtMostTwoLevelsDeep()
    {
        var admin = await LoginAdminAsync();
        var rootId = (await CreateCategoryAsync(admin, Unique("root"))).Id;
        var childId = (await CreateCategoryAsync(admin, Unique("child"), rootId)).Id;
        var otherRootId = (await CreateCategoryAsync(admin, Unique("other"))).Id;

        var third = await SendAsync(admin, HttpMethod.Post, "/api/v1/admin/categories",
            new { name = "سطح سوم", slug = Unique("third"), parentId = childId, sortOrder = 0, isActive = true });
        var moveParentUnderRoot = await SendAsync(admin, HttpMethod.Put, $"/api/v1/admin/categories/{rootId}",
            new { name = "ریشه", parentId = otherRootId, sortOrder = 0, isActive = true });
        var ownParent = await SendAsync(admin, HttpMethod.Put, $"/api/v1/admin/categories/{otherRootId}",
            new { name = "ریشه", parentId = otherRootId, sortOrder = 0, isActive = true });

        Assert.Contains("parentId", (await AssertProblemAsync(third, 400)).Errors!.Keys);
        Assert.Contains("parentId", (await AssertProblemAsync(moveParentUnderRoot, 400)).Errors!.Keys);
        Assert.Contains("parentId", (await AssertProblemAsync(ownParent, 400)).Errors!.Keys);
    }

    [Fact]
    public async Task DeletingUsedCategoryOrBrand_IsAConflict_UnusedCanBeDeleted()
    {
        var admin = await LoginAdminAsync();
        var categoryId = (await CreateCategoryAsync(admin, Unique("cat"))).Id;
        var brandId = (await CreateBrandAsync(admin, Unique("brand"))).Id;
        await CreateProductAsync(admin, categoryId, Unique("p"), brandId);
        var emptyId = (await CreateCategoryAsync(admin, Unique("empty"))).Id;

        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Delete, $"/api/v1/admin/categories/{categoryId}"), 409, "category_not_empty");
        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Delete, $"/api/v1/admin/brands/{brandId}"), 409, "brand_in_use");
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(admin, HttpMethod.Delete, $"/api/v1/admin/categories/{emptyId}")).StatusCode);
    }

    [Fact]
    public async Task DuplicateSlugsAndSkus_AreConflicts()
    {
        var admin = await LoginAdminAsync();
        var categorySlug = Unique("cat");
        var categoryId = (await CreateCategoryAsync(admin, categorySlug)).Id;
        var product = await CreateProductAsync(admin, categoryId, Unique("p"));

        var sameCategory = await SendAsync(admin, HttpMethod.Post, "/api/v1/admin/categories",
            new { name = "تکراری", slug = categorySlug, sortOrder = 0, isActive = true });
        var sameProduct = await SendAsync(admin, HttpMethod.Post, "/api/v1/admin/products", new
        {
            categoryId, name = "تکراری", slug = product.Slug, status = "draft",
            variants = new[] { new { sku = Unique("sku"), price = 1000, stock = 1, isActive = true, sortOrder = 0 } },
        });
        var sameSku = await SendAsync(admin, HttpMethod.Post, $"/api/v1/admin/products/{product.Id}/variants",
            new { sku = product.Variants[0].Sku.ToUpperInvariant(), price = 1000, stock = 1, isActive = true, sortOrder = 1 });

        await AssertProblemAsync(sameCategory, 409, "slug_taken");
        await AssertProblemAsync(sameProduct, 409, "slug_taken");
        await AssertProblemAsync(sameSku, 409, "sku_taken");
    }

    [Fact]
    public async Task InvalidSlug_And_InvalidPrices_AreValidationErrors()
    {
        var admin = await LoginAdminAsync();
        var categoryId = (await CreateCategoryAsync(admin, Unique("cat"))).Id;

        var badSlug = await SendAsync(admin, HttpMethod.Post, "/api/v1/admin/brands", new { name = "برند", slug = "Bad Slug", isActive = true });
        var badPrice = await SendAsync(admin, HttpMethod.Post, "/api/v1/admin/products", new
        {
            categoryId, name = "محصول", slug = Unique("p"), status = "draft",
            variants = new[] { new { sku = Unique("sku"), price = 1000, compareAtPrice = 900, stock = -1, isActive = true, sortOrder = 0 } },
        });
        var noVariants = await SendAsync(admin, HttpMethod.Post, "/api/v1/admin/products",
            new { categoryId, name = "محصول", slug = Unique("p"), status = "draft", variants = Array.Empty<object>() });

        Assert.Contains("Slug", (await AssertProblemAsync(badSlug, 400)).Errors!.Keys);
        var priceErrors = (await AssertProblemAsync(badPrice, 400)).Errors!.Keys;
        Assert.Contains(priceErrors, k => k.EndsWith("CompareAtPrice", StringComparison.Ordinal));
        Assert.Contains(priceErrors, k => k.EndsWith("Stock", StringComparison.Ordinal));
        Assert.Contains("variants", (await AssertProblemAsync(noVariants, 400)).Errors!.Keys);
    }

    [Fact]
    public async Task Names_AreNormalizedWhenSaved()
    {
        var admin = await LoginAdminAsync();
        var categoryId = (await CreateCategoryAsync(admin, Unique("cat"))).Id;

        var product = await CreateProductAsync(admin, categoryId, Unique("p"), name: "  لامپ   شمعي   كوچك ");

        Assert.Equal("لامپ شمعی کوچک", product.Name);
    }

    [Fact]
    public async Task Slug_CannotBeChangedByAnUpdate()
    {
        var admin = await LoginAdminAsync();
        var categoryId = (await CreateCategoryAsync(admin, Unique("cat"))).Id;
        var product = await CreateProductAsync(admin, categoryId, Unique("p"));

        var updated = await SendOkAsync<AdminProductBody>(admin, HttpMethod.Put, $"/api/v1/admin/products/{product.Id}",
            new { categoryId, name = "نام جدید", slug = "a-new-slug", status = "published" });

        Assert.Equal(product.Slug, updated.Slug);
        Assert.Equal("نام جدید", updated.Name);
    }

    [Fact]
    public async Task VariantUpdate_WithStaleRowVersion_IsAConflict()
    {
        var admin = await LoginAdminAsync();
        var categoryId = (await CreateCategoryAsync(admin, Unique("cat"))).Id;
        var v = (await CreateProductAsync(admin, categoryId, Unique("p"))).Variants[0];

        var first = await SendOkAsync<AdminVariantBody>(admin, HttpMethod.Put, $"/api/v1/admin/variants/{v.Id}",
            new { v.Sku, price = 120_000, v.CompareAtPrice, stock = 7, v.IsActive, v.SortOrder, v.RowVersion });
        var stale = await SendAsync(admin, HttpMethod.Put, $"/api/v1/admin/variants/{v.Id}",
            new { v.Sku, price = 130_000, v.CompareAtPrice, stock = 9, v.IsActive, v.SortOrder, v.RowVersion });

        Assert.Equal(7, first.Stock);
        Assert.NotEqual(v.RowVersion, first.RowVersion);
        await AssertProblemAsync(stale, 409);
    }

    [Fact]
    public async Task Attributes_RejectOptionsOfAnotherAttribute_AndForeignVariants()
    {
        var admin = await LoginAdminAsync();
        var attrs = await AttributesAsync(admin);
        var categoryId = (await CreateCategoryAsync(admin, Unique("cat"))).Id;
        var product = await CreateProductAsync(admin, categoryId, Unique("p"));
        var other = await CreateProductAsync(admin, categoryId, Unique("o"));
        var url = $"/api/v1/admin/products/{product.Id}/attributes";

        var wrongOption = await SendAsync(admin, HttpMethod.Put, url, new
        {
            values = new[] { new { attributeId = attrs["wattage"].Id, variantId = (int?)null, optionId = (int?)attrs["cct"].Options[0].Id, text = (string?)null } },
        });
        var foreignVariant = await SendAsync(admin, HttpMethod.Put, url, new { values = new[] { Opt(attrs, "wattage", "9w", other.Variants[0].Id) } });
        var bothLevels = await SendAsync(admin, HttpMethod.Put, url,
            new { values = new[] { Opt(attrs, "wattage", "9w"), Opt(attrs, "wattage", "12w", product.Variants[0].Id) } });

        Assert.Contains("values[0]", (await AssertProblemAsync(wrongOption, 400)).Errors!.Keys);
        Assert.Contains("values[0]", (await AssertProblemAsync(foreignVariant, 400)).Errors!.Keys);
        Assert.Contains("values", (await AssertProblemAsync(bothLevels, 400)).Errors!.Keys);
    }

    [Fact]
    public async Task AttributeOptions_CanBeAdded_ButNotTwice()
    {
        var admin = await LoginAdminAsync();
        var wattage = (await AttributesAsync(admin))["wattage"];
        var slug = Unique("w");

        var created = await SendAsync(admin, HttpMethod.Post, $"/api/v1/admin/attributes/{wattage.Id}/options", new { value = "۳۰ وات", slug, sortOrder = 99 });
        var duplicate = await SendAsync(admin, HttpMethod.Post, $"/api/v1/admin/attributes/{wattage.Id}/options", new { value = "۳۰ وات", slug, sortOrder = 99 });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        await AssertProblemAsync(duplicate, 409, "option_slug_taken");
    }

    [Fact]
    public async Task CctOptions_UseCustomerSearchTerms()
    {
        var cct = (await AttributesAsync(await LoginAdminAsync()))["cct"];

        Assert.Equal(
            new Dictionary<string, string>
            {
                ["2700k"] = "آفتابی (۲۷۰۰K)", ["3000k"] = "آفتابی (۳۰۰۰K)", ["4000k"] = "یخی/طبیعی (۴۰۰۰K)", ["6500k"] = "مهتابی (۶۵۰۰K)",
            },
            cct.Options.Where(o => o.Slug.EndsWith("00k", StringComparison.Ordinal)).ToDictionary(o => o.Slug, o => o.Value));
    }

    [Fact]
    public async Task BrandRename_UpdatesProductSearch()
    {
        var admin = await LoginAdminAsync();
        var categorySlug = Unique("cat");
        var categoryId = (await CreateCategoryAsync(admin, categorySlug)).Id;
        var brandSlug = Unique("brand");
        var brandId = (await CreateBrandAsync(admin, brandSlug)).Id;
        var product = await CreateProductAsync(admin, categoryId, Unique("p"), brandId);
        var newName = "نورافشان " + Guid.NewGuid().ToString("N")[..6];

        await SendOkAsync<IdBody>(admin, HttpMethod.Put, $"/api/v1/admin/brands/{brandId}", new { name = newName, isActive = true });
        var found = await GetOkAsync<ProductListBody>(NewClient(), $"/api/v1/catalog/products?category={categorySlug}&q={Uri.EscapeDataString(newName)}");

        Assert.Equal([product.Slug], found.Items.Select(i => i.Slug));
    }

    [Fact]
    public async Task Images_AreUploadedServedReorderedAndDeleted_OnlyRealImagesAccepted()
    {
        var admin = await LoginAdminAsync();
        var categoryId = (await CreateCategoryAsync(admin, Unique("cat"))).Id;
        var product = await CreateProductAsync(admin, categoryId, Unique("p"));
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

        var first = await UploadAsync(admin, product.Id, png, "lamp.png", "image/png");
        var second = await UploadAsync(admin, product.Id, png, "lamp2.png", "image/png");
        var fake = await UploadAsync(admin, product.Id, "not an image"u8.ToArray(), "evil.png", "image/png");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var image = await ReadAsync<AdminImageBody>(first);
        var image2 = await ReadAsync<AdminImageBody>(second);
        Assert.StartsWith("/media/products/", image.Url, StringComparison.Ordinal);
        Assert.EndsWith(".png", image.Url, StringComparison.Ordinal);
        Assert.Equal("محصول " + product.Slug, image.Alt); // defaults to the product name
        Assert.Contains("file", (await AssertProblemAsync(fake, 400)).Errors!.Keys);

        var served = await NewClient().GetAsync(image.Url);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal(png, await served.Content.ReadAsByteArrayAsync());

        var reordered = await SendOkAsync<AdminProductBody>(admin, HttpMethod.Put, $"/api/v1/admin/products/{product.Id}/images/order",
            new { imageIds = new[] { image2.Id, image.Id } });
        Assert.Equal([image2.Id, image.Id], reordered.Images.Select(i => i.Id));

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(admin, HttpMethod.Delete, $"/api/v1/admin/images/{image.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await NewClient().GetAsync(image.Url)).StatusCode);
    }

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 0];
    private static readonly byte[] Webp = [.. "RIFF"u8, 4, 0, 0, 0, .. "WEBPVP8 "u8];

    private static async Task<int> NewProductIdAsync(HttpClient admin) =>
        (await CreateProductAsync(admin, (await CreateCategoryAsync(admin, Unique("cat"))).Id, Unique("p"))).Id;

    [Theory]
    [InlineData("jpeg", ".jpg")]
    [InlineData("png", ".png")]
    [InlineData("webp", ".webp")]
    public async Task Upload_AcceptsJpegPngWebp_ByContent_WithAServerGeneratedName(string format, string extension)
    {
        var admin = await LoginAdminAsync();
        var bytes = format switch { "jpeg" => Jpeg, "png" => Png, _ => Webp };

        // Client file name and content type are deliberately misleading: only the bytes decide.
        var response = await UploadAsync(admin, await NewProductIdAsync(admin), bytes, "../../evil name.php", "text/plain");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var url = (await ReadAsync<AdminImageBody>(response)).Url;
        Assert.Matches("^/media/products/[0-9]+/[0-9a-f]{32}" + System.Text.RegularExpressions.Regex.Escape(extension) + "$", url);
        var served = await NewClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal("nosniff", served.Headers.GetValues("X-Content-Type-Options").Single());
    }

    public static TheoryData<string, string, string> NonImages => new()
    {
        { """<svg xmlns="http://www.w3.org/2000/svg"><script>alert(1)</script></svg>""", "logo.svg", "image/svg+xml" },
        { """<?xml version="1.0"?><svg xmlns="http://www.w3.org/2000/svg"/>""", "logo.png", "image/png" },
        { "GIF89a\u0001\u0000\u0001\u0000", "anim.gif", "image/gif" },
        { "<html><script>alert(1)</script></html>", "page.jpg", "image/jpeg" },
        { "MZ\u0090\u0000", "tool.webp", "image/webp" },
    };

    [Theory]
    [MemberData(nameof(NonImages))]
    public async Task Upload_RejectsAnythingThatIsNotJpegPngOrWebp(string content, string fileName, string contentType)
    {
        var admin = await LoginAdminAsync();

        var response = await UploadAsync(admin, await NewProductIdAsync(admin), Encoding.UTF8.GetBytes(content), fileName, contentType);

        Assert.Contains("file", (await AssertProblemAsync(response, 400)).Errors!.Keys);
    }

    [Fact]
    public async Task Upload_DefaultLimitIsFiveMegabytes()
    {
        var admin = await LoginAdminAsync();
        var productId = await NewProductIdAsync(admin);
        var tooBig = new byte[5 * 1024 * 1024 + 1];
        Png.CopyTo(tooBig, 0);

        Assert.Equal(HttpStatusCode.Created, (await UploadAsync(admin, productId, tooBig[..^1], "big.png", "image/png")).StatusCode);
        await AssertProblemAsync(await UploadAsync(admin, productId, tooBig, "big.png", "image/png"), 400);
    }

    [Fact]
    public async Task Upload_LimitComesFromConfiguration()
    {
        using var factory = Fixture.Factory.WithWebHostBuilder(host => host.ConfigureTestServices(services =>
            services.PostConfigure<MediaOptions>(o => o.MaxImageBytes = 100)));
        var admin = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var login = await PostAsync(admin, "/api/v1/auth/login", new { email = ApiFixture.AdminEmail, password = Fixture.AdminPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var productId = await NewProductIdAsync(admin);
        var image = new byte[101];
        Png.CopyTo(image, 0);

        await AssertProblemAsync(await UploadAsync(admin, productId, image, "a.png", "image/png"), 400);
        Assert.Equal(HttpStatusCode.Created, (await UploadAsync(admin, productId, image[..100], "a.png", "image/png")).StatusCode);
    }

    [Fact]
    public async Task MediaFolder_OnlyServesImageTypes()
    {
        Directory.CreateDirectory(Fixture.MediaRoot);
        await File.WriteAllTextAsync(Path.Combine(Fixture.MediaRoot, "planted.html"), "<script>alert(1)</script>");
        await File.WriteAllTextAsync(Path.Combine(Fixture.MediaRoot, "planted.svg"), "<svg/>");

        Assert.Equal(HttpStatusCode.NotFound, (await NewClient().GetAsync("/media/planted.html")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await NewClient().GetAsync("/media/planted.svg")).StatusCode);
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, int productId, byte[] bytes, string fileName, string contentType)
    {
        var csrf = await client.GetFromJsonAsync<CsrfBody>("/api/v1/auth/csrf", Json);
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, "file", fileName);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/products/{productId}/images") { Content = content };
        request.Headers.Add(csrf!.HeaderName, csrf.Token);
        return await client.SendAsync(request);
    }
}
