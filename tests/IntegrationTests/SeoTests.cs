using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shop.Domain.Security;

namespace Shop.IntegrationTests;

public sealed record SeoFieldsBody(string? MetaTitle, string? MetaDescription, int? OgMediaId, string? OgImageUrl, bool NoIndex);

public sealed record FaqBody(string Question, string Answer);

public sealed record AdminSeoBody(
    string Type, int Id, string Name, string Path, SeoFieldsBody Seo, FaqBody[] Faq, string? Intro, bool SupportsFaq, bool SupportsIntro, byte[]? RowVersion);

public sealed record PublicSeoBody(string? Title, string? Description, string? OgImageUrl, bool NoIndex, FaqBody[] Faq, string? Intro);

public sealed record NoIndexBody(string[] Product, string[] Category, string[] ContentPage, string[] BlogPost);

/// <summary>Per-entity SEO fields, FAQ and category guide, their permissions and validation, the public read, and the site-level tags (docs/17).</summary>
public sealed class SeoTests(ApiFixture fixture) : CatalogTestBase(fixture)
{
    private static string Url(string type, int id) => $"/api/v1/admin/seo/{type}/{id}";

    private static object Save(string? title = null, string? description = null, int? og = null, bool noIndex = false, object[]? faq = null, string? intro = null, byte[]? rowVersion = null) =>
        new { metaTitle = title, metaDescription = description, ogMediaId = og, noIndex, faq, intro, rowVersion };

    private async Task<(int CategoryId, string CategorySlug, int ProductId, string ProductSlug, int PostId, string PostSlug, int PageId)> EntitiesAsync(HttpClient admin)
    {
        var categorySlug = Unique("seo-cat");
        var category = await CreateCategoryAsync(admin, categorySlug);
        var productSlug = Unique("seo-prod");
        var product = await CreateProductAsync(admin, category.Id, productSlug);
        var postSlug = Unique("seo-post");
        var post = await SendOkAsync<AdminBlogBody>(admin, HttpMethod.Post, "/api/v1/admin/blog", new { slug = postSlug, title = "نوشته", excerpt = "خلاصه", body = "متن", publish = true });
        var page = await GetOkAsync<AdminContentBody>(admin, "/api/v1/admin/content-pages/about");
        return (category.Id, categorySlug, product.Id, productSlug, post.Id, postSlug, page.Id);
    }

    [Fact]
    public async Task AnEntityWithoutSeoRows_ReadsAsEmptyDefaults_AndTheFlagsSayWhatEachTypeSupports()
    {
        var admin = await LoginAdminAsync();
        var e = await EntitiesAsync(admin);

        var product = await GetOkAsync<AdminSeoBody>(admin, Url("product", e.ProductId));
        Assert.True(product.SupportsFaq);
        Assert.False(product.SupportsIntro);
        Assert.Null(product.RowVersion); // no row yet: the first save creates it
        Assert.Equal($"/product/{e.ProductSlug}", product.Path);
        Assert.Equal((null, null, false), (product.Seo.MetaTitle, product.Seo.MetaDescription, product.Seo.NoIndex));
        Assert.Empty(product.Faq);

        var category = await GetOkAsync<AdminSeoBody>(admin, Url("category", e.CategoryId));
        Assert.True(category.SupportsFaq && category.SupportsIntro);
        var post = await GetOkAsync<AdminSeoBody>(admin, Url("blogPost", e.PostId));
        Assert.False(post.SupportsFaq || post.SupportsIntro);
        var page = await GetOkAsync<AdminSeoBody>(admin, Url("contentPage", e.PageId));
        Assert.False(page.SupportsFaq || page.SupportsIntro);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(Url("product", 99_999_999))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync(Url("mystery", 1))).StatusCode);
    }

    [Fact]
    public async Task SavingNormalizesAndStoresTheFields_TheStorefrontReadsThem_AndNoIndexIsListed()
    {
        var admin = await LoginAdminAsync();
        var e = await EntitiesAsync(admin);

        var saved = await SendOkAsync<AdminSeoBody>(admin, HttpMethod.Put, Url("product", e.ProductId),
            Save(title: "  لامپ   حبابی ۹ وات  ", description: "توضیح كوتاه", noIndex: true, faq:
            [
                new { question = "ضمانت دارد؟", answer = "بله، ۱۲ ماه." },
                new { question = "سرپیچ؟", answer = "E27" },
            ]));
        Assert.Equal(("لامپ حبابی ۹ وات", "توضیح کوتاه", true), (saved.Seo.MetaTitle, saved.Seo.MetaDescription, saved.Seo.NoIndex));
        Assert.Equal(["ضمانت دارد؟", "سرپیچ؟"], saved.Faq.Select(f => f.Question));
        Assert.True(saved.RowVersion is { Length: > 0 });

        var anon = NewClient();
        var pub = await GetOkAsync<PublicSeoBody>(anon, $"/api/v1/catalog/seo/product/{e.ProductSlug}");
        Assert.Equal(("لامپ حبابی ۹ وات", true, 2), (pub.Title, pub.NoIndex, pub.Faq.Length));
        var noindex = await GetOkAsync<NoIndexBody>(anon, "/api/v1/catalog/seo/noindex");
        Assert.Contains(e.ProductSlug, noindex.Product);

        // Turning it back off removes it from the list; clearing the fields makes them "generated default" again (null).
        var cleared = await SendOkAsync<AdminSeoBody>(admin, HttpMethod.Put, Url("product", e.ProductId), Save(rowVersion: saved.RowVersion));
        Assert.Equal((null, null, false), (cleared.Seo.MetaTitle, cleared.Seo.MetaDescription, cleared.Seo.NoIndex));
        Assert.Empty(cleared.Faq);
        Assert.DoesNotContain(e.ProductSlug, (await GetOkAsync<NoIndexBody>(anon, "/api/v1/catalog/seo/noindex")).Product);
    }

    [Fact]
    public async Task ACategoryTakesAGuideInMarkdown_PostsAndPagesTakeNeitherFaqNorGuide()
    {
        var admin = await LoginAdminAsync();
        var e = await EntitiesAsync(admin);

        var saved = await SendOkAsync<AdminSeoBody>(admin, HttpMethod.Put, Url("category", e.CategoryId),
            Save(intro: "## راهنمای خرید\n\nلامپ **کم‌مصرف** بخرید.", faq: [new { question = "چطور انتخاب کنم؟", answer = "توان را ببینید." }]));
        Assert.Equal("## راهنمای خرید\n\nلامپ **کم‌مصرف** بخرید.", saved.Intro);
        var pub = await GetOkAsync<PublicSeoBody>(NewClient(), $"/api/v1/catalog/seo/category/{e.CategorySlug}");
        Assert.Equal(("## راهنمای خرید\n\nلامپ **کم‌مصرف** بخرید.", 1), (pub.Intro, pub.Faq.Length));

        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Put, Url("blogPost", e.PostId), Save(intro: "x")), 400);
        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Put, Url("contentPage", e.PageId), Save(faq: [new { question = "q", answer = "a" }])), 400);
        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Put, Url("product", e.ProductId), Save(intro: "x")), 400);
    }

    [Fact]
    public async Task Validation_Lengths_FaqRules_AndTheShareImage()
    {
        var admin = await LoginAdminAsync();
        var e = await EntitiesAsync(admin);

        async Task<Dictionary<string, string[]>> Errors(object body) => (await AssertProblemAsync(await SendAsync(admin, HttpMethod.Put, Url("product", e.ProductId), body), 400)).Errors!;

        Assert.Contains("metaTitle", (await Errors(Save(title: new string('ب', 121)))).Keys);
        Assert.Contains("metaDescription", (await Errors(Save(description: new string('ب', 321)))).Keys);
        Assert.Contains("ogMediaId", (await Errors(Save(og: 999_999))).Keys);
        Assert.Contains("faq[0].question", (await Errors(Save(faq: [new { question = "", answer = "a" }]))).Keys);
        Assert.Contains("faq[0].answer", (await Errors(Save(faq: [new { question = "q", answer = new string('ب', 2001) }]))).Keys);
        Assert.Contains("faq", (await Errors(Save(faq: Enumerable.Range(0, 21).Select(i => (object)new { question = $"q{i}", answer = "a" }).ToArray()))).Keys);

        // Boundaries are accepted.
        await SendOkAsync<AdminSeoBody>(admin, HttpMethod.Put, Url("product", e.ProductId), Save(title: new string('ب', 120), description: new string('ب', 320)));
    }

    [Fact]
    public async Task AStaleVersionIs409_AndEachTypeNeedsItsOwnPermission()
    {
        var admin = await LoginAdminAsync();
        var e = await EntitiesAsync(admin);

        var first = await SendOkAsync<AdminSeoBody>(admin, HttpMethod.Put, Url("product", e.ProductId), Save(title: "یک"));
        await SendOkAsync<AdminSeoBody>(admin, HttpMethod.Put, Url("product", e.ProductId), Save(title: "دو", rowVersion: first.RowVersion));
        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Put, Url("product", e.ProductId), Save(title: "سه", rowVersion: first.RowVersion)), 409, "concurrent_update");
        // A first save that already carries a version for a row that does not exist is stale too.
        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Put, Url("category", e.CategoryId), Save(title: "x", rowVersion: new byte[8] { 1, 0, 0, 0, 0, 0, 0, 0 })), 409, "concurrent_update");

        // A blogger may edit posts' SEO but not a product's, a catalog editor not a post's.
        var blogger = await SignedInWithAsync(admin, Permissions.BlogManage);
        Assert.Equal(HttpStatusCode.OK, (await blogger.GetAsync(Url("blogPost", e.PostId))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await blogger.GetAsync(Url("product", e.ProductId))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(blogger, HttpMethod.Put, Url("category", e.CategoryId), Save())).StatusCode);
        var catalog = await SignedInWithAsync(admin, Permissions.CatalogManage);
        Assert.Equal(HttpStatusCode.OK, (await catalog.GetAsync(Url("category", e.CategoryId))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await catalog.GetAsync(Url("blogPost", e.PostId))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await NewClient().GetAsync(Url("product", e.ProductId))).StatusCode);
    }

    private async Task<HttpClient> SignedInWithAsync(HttpClient admin, string permission)
    {
        var role = await SendOkAsync<StaffRoleBody>(admin, HttpMethod.Post, "/api/v1/admin/staff/roles",
            new { name = "Seo" + Guid.NewGuid().ToString("N")[..8], title = "نقش آزمایشی", permissions = new[] { permission }, currentPassword = Fixture.AdminPassword });
        var username = "seo-" + Guid.NewGuid().ToString("N")[..8];
        var password = TestData.NewPassword() + "-Zz9";
        await SendOkAsync<StaffUserBody>(admin, HttpMethod.Post, "/api/v1/admin/staff",
            new { username, displayName = "x", email = (string?)null, password, roles = new[] { role.Name }, currentPassword = Fixture.AdminPassword });
        var client = NewClient();
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(client, "/api/v1/auth/login", new { username, password })).StatusCode);
        return client;
    }

    [Fact]
    public async Task ThePublicRead_IsAn404ForUnknownThings_AndEmptyForAnEntityWithoutRows()
    {
        var admin = await LoginAdminAsync();
        var e = await EntitiesAsync(admin);
        var anon = NewClient();
        var empty = await GetOkAsync<PublicSeoBody>(anon, $"/api/v1/catalog/seo/blogPost/{e.PostSlug}");
        Assert.Equal((null, null, false, 0), (empty.Title, empty.Description, empty.NoIndex, empty.Faq.Length));
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/api/v1/catalog/seo/product/does-not-exist")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync("/api/v1/catalog/seo/mystery/x")).StatusCode);
    }

    // ---- site level: Search Console token and GA4 id --------------------------------------------------------------------------

    private static object SeoPayload(string token = "", string ga4 = "") => new
    {
        siteName = "قصر روشنایی", titleTemplate = "%s | قصر روشنایی", homeTitle = "عنوان", defaultDescription = "توضیح", defaultOgMediaId = (int?)null,
        searchConsoleToken = token, ga4Id = ga4,
    };

    [Fact]
    public async Task TheSearchConsoleToken_AndGa4Id_AreValidated_PublishedInTheSiteDocument_AndClearable()
    {
        var admin = await LoginAdminAsync();
        const string url = "/api/v1/admin/site/seo";
        var current = await GetOkAsync<SiteKindBody>(admin, url);
        if (current.Draft is { } stale)
        {
            await SendOkAsync<SiteKindBody>(admin, HttpMethod.Post, url + "/discard", new { rowVersion = stale.RowVersion });
        }

        // Bad shapes are refused with field errors (nothing that could break out of a tag or a script is ever stored).
        foreach (var (token, ga4, field) in new[] { ("short", "", "searchConsoleToken"), ("a\"><script>alert(1)</script>aaaaaaaaaa", "", "searchConsoleToken"), ("", "UA-12345-1", "ga4Id"), ("", "G-<script>", "ga4Id") })
        {
            var refused = await AssertProblemAsync(await SendAsync(admin, HttpMethod.Put, url + "/draft", new { payload = SeoPayload(token, ga4) }), 400);
            Assert.Contains(field, refused.Errors!.Keys);
        }

        const string validToken = "abcDEF1234567890_-abcDEF12";
        var saved = await SendOkAsync<SiteKindBody>(admin, HttpMethod.Put, url + "/draft", new { payload = SeoPayload(validToken, "g-abc123xyz") });
        Assert.Equal("G-ABC123XYZ", saved.Draft!.Payload.GetProperty("ga4Id").GetString()); // normalized to upper case
        var published = await SendOkAsync<SiteKindBody>(admin, HttpMethod.Post, url + "/publish", new { rowVersion = saved.Draft.RowVersion });
        Assert.NotNull(published.Published);

        var site = await GetOkAsync<JsonElement>(NewClient(), "/api/v1/catalog/site");
        Assert.Equal((validToken, "G-ABC123XYZ"), (site.GetProperty("seo").GetProperty("searchConsoleToken").GetString(), site.GetProperty("seo").GetProperty("ga4Id").GetString()));

        // Cleared again: both are empty, so the storefront renders neither tag nor script.
        var cleared = await SendOkAsync<SiteKindBody>(admin, HttpMethod.Put, url + "/draft", new { payload = SeoPayload() });
        await SendOkAsync<SiteKindBody>(admin, HttpMethod.Post, url + "/publish", new { rowVersion = cleared.Draft!.RowVersion });
        var after = await GetOkAsync<JsonElement>(NewClient(), "/api/v1/catalog/site");
        Assert.Equal(("", ""), (after.GetProperty("seo").GetProperty("searchConsoleToken").GetString(), after.GetProperty("seo").GetProperty("ga4Id").GetString()));
    }
}
