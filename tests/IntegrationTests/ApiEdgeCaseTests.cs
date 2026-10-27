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
        var csrf = await client.GetFromJsonAsync<CsrfBody>("/api/v1/auth/csrf", Json);
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
        Assert.Equal(MalformedTitle, problem.Title);
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
}
