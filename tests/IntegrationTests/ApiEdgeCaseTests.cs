using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace Shop.IntegrationTests;

/// <summary>
/// How the API answers requests that are wrong in framework-level ways (binding, content type, routing, concurrency).
/// These were written against the minimal-API implementation and pin the answers the controllers must keep giving.
/// </summary>
public sealed class ApiEdgeCaseTests(ApiFixture fixture) : CatalogTestBase(fixture)
{
    /// <summary>The framework answers an unbindable request with a bare 400; the status-code pages give it this title.</summary>
    private const string MalformedTitle = "درخواست نامعتبر است.";

    private static async Task<HttpResponseMessage> RawPostAsync(HttpClient client, string url, HttpContent? content)
    {
        var csrf = await CsrfTokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.Add(csrf!.HeaderName, csrf.Token);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task MalformedJson_Is400_WithThePersianMalformedTitle()
    {
        var response = await RawPostAsync(NewClient(), "/api/v1/auth/login", new StringContent("{ not json", Encoding.UTF8, "application/json"));

        var problem = await AssertProblemAsync(response, 400);
        Assert.Equal(MalformedTitle, problem.Title);
        Assert.Null(problem.Errors);
    }

    [Fact]
    public async Task WrongTypeInsideJson_Is400_WithThePersianMalformedTitle()
    {
        var response = await RawPostAsync(NewClient(), "/api/v1/auth/login", new StringContent("""{"username": 5, "password": [1]}""", Encoding.UTF8, "application/json"));

        var problem = await AssertProblemAsync(response, 400);
        Assert.Equal(MalformedTitle, problem.Title);
    }

    [Fact]
    public async Task EmptyBody_OnABodyEndpoint_Is400_WithThePersianMalformedTitle()
    {
        var response = await RawPostAsync(NewClient(), "/api/v1/auth/login", null);

        var problem = await AssertProblemAsync(response, 400);
        Assert.Equal("بدنهٔ درخواست لازم است.", problem.ErrorMessage);
    }

    [Fact]
    public async Task WrongContentType_Is415_InPersian()
    {
        var response = await RawPostAsync(NewClient(), "/api/v1/auth/login", new StringContent("username=a", Encoding.UTF8, "text/plain"));

        var problem = await AssertProblemAsync(response, 415);
        Assert.Contains("پشتیبانی نمی‌شود", problem.Title);
    }

    [Fact]
    public async Task UnknownRoute_Is404_AndWrongMethod_Is405_BothInPersian()
    {
        var client = NewClient();

        var unknown = await AssertProblemAsync(await client.GetAsync("/api/v1/no-such-thing"), 404);
        var wrongMethod = await AssertProblemAsync(await client.GetAsync("/api/v1/auth/login"), 405);

        Assert.Equal("پیدا نشد.", unknown.Title);
        Assert.Equal("روش درخواست مجاز نیست.", wrongMethod.Title);
    }

    [Fact]
    public async Task ValidationFailure_Is400_WithPerFieldErrors()
    {
        var response = await PostAsync(NewClient(), "/api/v1/auth/login", new { });

        var problem = await AssertProblemAsync(response, 400);
        Assert.NotNull(problem.Errors);
        Assert.NotEmpty(problem.Errors!);
        Assert.Equal("یک یا چند مقدار نامعتبر است.", problem.Title);
    }

    [Fact]
    public async Task StaleRowVersion_Is409_WithTheConcurrentUpdateProblem()
    {
        var admin = await LoginAdminAsync();
        var page = await GetOkAsync<AdminPageBody>(admin, "/api/v1/admin/content-pages/about");

        var first = await SendAsync(admin, HttpMethod.Put, "/api/v1/admin/content-pages/about", new { title = page.Title, body = page.Body + "\n\nمتن آزمایشی", rowVersion = page.RowVersion });
        Assert.True(first.IsSuccessStatusCode, await first.Content.ReadAsStringAsync());
        var stale = await SendAsync(admin, HttpMethod.Put, "/api/v1/admin/content-pages/about", new { title = page.Title, body = page.Body + "\n\nمتن دیگر", rowVersion = page.RowVersion });

        // Put the seeded draft back so other tests see the page they expect.
        var current = await GetOkAsync<AdminPageBody>(admin, "/api/v1/admin/content-pages/about");
        var restore = await SendAsync(admin, HttpMethod.Put, "/api/v1/admin/content-pages/about", new { title = page.Title, body = page.Body, rowVersion = current.RowVersion });
        Assert.True(restore.IsSuccessStatusCode, await restore.Content.ReadAsStringAsync());

        await AssertProblemAsync(stale, 409);
    }

    [Theory]
    [InlineData("/api/v1/admin/products?status=bogus")]
    [InlineData("/api/v1/admin/products?page=abc")]
    [InlineData("/api/v1/admin/orders?page=abc")]
    public async Task UnparsableQueryValue_Is400_WithThePersianMalformedTitle(string url)
    {
        var admin = await LoginAdminAsync();

        var problem = await AssertProblemAsync(await admin.GetAsync(url), 400);

        Assert.Equal(MalformedTitle, problem.Title);
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("Published")]
    [InlineData("Archived")]
    public async Task AdminProductList_AcceptsEveryStatusName(string status)
    {
        var admin = await LoginAdminAsync();

        var response = await admin.GetAsync($"/api/v1/admin/products?status={status}");

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("draft")]
    [InlineData("published")]
    [InlineData("archived")]
    public async Task AdminProductList_StatusNamesAreCaseSensitive(string status)
    {
        var admin = await LoginAdminAsync();

        await AssertProblemAsync(await admin.GetAsync($"/api/v1/admin/products?status={status}"), 400);
    }

    [Fact]
    public async Task AdminOrderFilter_WithAnUnknownStatus_Is400_WithAFilterError()
    {
        var admin = await LoginAdminAsync();

        var problem = await AssertProblemAsync(await admin.GetAsync("/api/v1/admin/orders?status=nonsense"), 400);

        Assert.NotNull(problem.Errors);
        Assert.Contains("filter", problem.Errors!.Keys);
    }

    [Fact]
    public async Task ProductListQuery_OfTheCatalog_IsCaseInsensitiveForEnumsAndRejectsBadNumbers()
    {
        var client = NewClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/catalog/products?sort=newest&page=1")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/catalog/products?page=abc")).StatusCode);
    }

    [Fact]
    public async Task ImageUpload_WithoutAFile_Is400_AndAMissingProductIsNotFound()
    {
        var admin = await LoginAdminAsync();
        using var emptyForm = new MultipartFormDataContent { { new StringContent("alt"), "alt" } };

        var missingFile = await RawPostAsync(admin, "/api/v1/admin/products/999999/images", emptyForm);
        await AssertProblemAsync(missingFile, 400);

        using var form = new MultipartFormDataContent { { new ByteArrayContent([1, 2, 3]), "file", "a.png" } };
        var notFound = await RawPostAsync(admin, "/api/v1/admin/products/999999/images", form);
        Assert.True(notFound.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest, $"{(int)notFound.StatusCode}");
    }

    [Fact]
    public async Task UnsafeRequestWithoutCsrfToken_Is400_AntiforgeryInvalid()
    {
        var admin = await LoginAdminAsync();

        var response = await admin.PostAsync("/api/v1/admin/orders/999999/cancel", null);

        await AssertProblemAsync(response, 400, "antiforgery_invalid");
    }

    [Fact]
    public async Task StorefrontEndpoints_NeedTheKey_AndNoCsrfToken()
    {
        var client = NewClient();

        var withoutKey = await client.GetAsync("/api/v1/store/cart");
        await AssertProblemAsync(withoutKey, 401, "storefront_key_invalid");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/store/cart");
        request.Headers.Add("X-Storefront-Key", ApiFixture.StorefrontKey);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(request)).StatusCode);
    }

    private static async Task<HttpResponseMessage> JsonAsync(HttpClient client, HttpMethod method, string url, string? body, string contentType = "application/json")
    {
        var csrf = await CsrfTokenAsync(client);
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8);
            request.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType);
        }

        request.Headers.Add(csrf!.HeaderName, csrf.Token);
        return await client.SendAsync(request);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("")]
    public async Task NullOrEmptyBody_OnAValidatedEndpoint_Is400_WithTheBodyRequiredDetail(string body)
    {
        var response = await JsonAsync(NewClient(), HttpMethod.Post, "/api/v1/auth/login", body);

        var problem = await AssertProblemAsync(response, 400);
        Assert.Equal("بدنهٔ درخواست لازم است.", problem.ErrorMessage);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"x\"")]
    [InlineData("5")]
    [InlineData("  ")]
    [InlineData("{\"username\":\"a\"")]
    public async Task UnreadableJson_Is400_WithNoDetail(string body)
    {
        var problem = await AssertProblemAsync(await JsonAsync(NewClient(), HttpMethod.Post, "/api/v1/auth/login", body), 400);

        Assert.Equal(MalformedTitle, problem.ErrorMessage);
    }

    [Theory]
    [InlineData("application/json; charset=utf-8")]
    [InlineData("application/vnd.shop+json")]
    public async Task JsonMediaTypeVariants_AreAccepted(string contentType)
    {
        var problem = await AssertProblemAsync(await JsonAsync(NewClient(), HttpMethod.Post, "/api/v1/auth/login", "{}", contentType), 400);

        Assert.Equal("یک یا چند مقدار نامعتبر است.", problem.Title);
    }

    [Theory]
    [InlineData("<a/>", "application/xml")]
    [InlineData("a=b", "application/x-www-form-urlencoded")]
    public async Task UnsupportedBodyMediaType_Is415(string body, string contentType) =>
        await AssertProblemAsync(await JsonAsync(NewClient(), HttpMethod.Post, "/api/v1/auth/login", body, contentType), 415);

    [Theory]
    [InlineData("POST", "/api/v1/admin/orders/999999/status", "null")]
    [InlineData("POST", "/api/v1/admin/orders/999999/status", "{\"status\":\"zzz\"}")]
    [InlineData("PUT", "/api/v1/admin/settings", "null")]
    [InlineData("PUT", "/api/v1/admin/settings", null)]
    public async Task EndpointsWithoutAValidator_AnswerABareBadRequest_ForAnUnusableBody(string method, string url, string? body)
    {
        var admin = await LoginAdminAsync();

        var problem = await AssertProblemAsync(await JsonAsync(admin, new HttpMethod(method), url, body), 400);

        Assert.Equal(MalformedTitle, problem.ErrorMessage);
    }

    [Fact]
    public async Task OrderStatusBody_AcceptsANumber_AndAnEmptyObjectIsAnIllegalTransition()
    {
        var admin = await LoginAdminAsync();

        await AssertProblemAsync(await JsonAsync(admin, HttpMethod.Post, "/api/v1/admin/orders/999999/status", "{\"status\":2}"), 404);
        await AssertProblemAsync(await JsonAsync(admin, HttpMethod.Post, "/api/v1/admin/orders/999999/status", "{}"), 409);
    }

    [Theory]
    [InlineData("status=Published", 200)]
    [InlineData("status=1", 200)]
    [InlineData("status=99", 200)]
    [InlineData("status=published", 400)] // enum names are case-sensitive in a query string
    [InlineData("status=PUBLISHED", 400)]
    [InlineData("status=", 400)]
    [InlineData("status=Published&status=Draft", 200)] // repeated values are joined with commas, which an enum parse accepts
    [InlineData("page=0", 200)]
    [InlineData("page=-1", 200)]
    [InlineData("page=1.5", 400)]
    [InlineData("page=99999999999", 400)]
    [InlineData("page=1&page=2", 400)]
    [InlineData("page=%20", 400)]
    public async Task AdminProductList_QueryBinding_FollowsTheBuiltInRules(string query, int expected)
    {
        var admin = await LoginAdminAsync();

        Assert.Equal(expected, (int)(await admin.GetAsync("/api/v1/admin/products?" + query)).StatusCode);
    }

    [Theory]
    [InlineData("", 200)]
    [InlineData("page=0", 200)]
    [InlineData("page=-2", 200)]
    [InlineData("page=abc", 400)]
    [InlineData("page=", 400)]
    public async Task PublicBlogList_PageBinding_FollowsTheBuiltInRules(string query, int expected) =>
        Assert.Equal(expected, (int)(await NewClient().GetAsync("/api/v1/catalog/blog?" + query)).StatusCode);

    [Theory]
    [InlineData("page=0", 400)]
    [InlineData("pageSize=1000", 400)]
    [InlineData("sort=zzz", 400)]
    [InlineData("inStock=maybe", 400)]
    [InlineData("minPrice=abc", 400)]
    [InlineData("q=", 200)]
    public async Task PublicProductList_RejectsBadValues_WithAValidationProblem(string query, int expected)
    {
        var response = await NewClient().GetAsync("/api/v1/catalog/products?" + query);

        Assert.Equal(expected, (int)response.StatusCode);
        if (expected == 400)
        {
            await AssertProblemAsync(response, 400);
        }
    }

    [Fact]
    public async Task UploadWithABodyThatIsNotMultipart_Is415_AndWithNoBody_Is400()
    {
        var admin = await LoginAdminAsync();

        await AssertProblemAsync(await JsonAsync(admin, HttpMethod.Post, "/api/v1/admin/products/1/images", "x", "text/plain"), 415);
        await AssertProblemAsync(await JsonAsync(admin, HttpMethod.Post, "/api/v1/admin/products/1/images", null), 400);
    }

    [Fact]
    public async Task HeadAndOptions_OnAGetEndpoint_Are405_AndRoutingIgnoresCaseAndTrailingSlash()
    {
        var client = NewClient();

        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/api/v1/catalog/categories"))).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.SendAsync(new HttpRequestMessage(HttpMethod.Options, "/api/v1/catalog/categories"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/API/V1/Catalog/Categories")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/catalog/categories/")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/catalog/blog/")).StatusCode);
    }
}
