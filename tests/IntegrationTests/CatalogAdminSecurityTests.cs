using System.Net.Http.Json;

namespace Shop.IntegrationTests;

/// <summary>Every admin catalog endpoint: anonymous → 401, customer → 403; every admin write without a CSRF token → 400.</summary>
public sealed class CatalogAdminSecurityTests(ApiFixture fixture) : CatalogTestBase(fixture)
{
    private static readonly (string Method, string Url)[] All =
    [
        ("GET", "/api/v1/admin/categories"),
        ("POST", "/api/v1/admin/categories"),
        ("PUT", "/api/v1/admin/categories/1"),
        ("DELETE", "/api/v1/admin/categories/1"),
        ("GET", "/api/v1/admin/brands"),
        ("POST", "/api/v1/admin/brands"),
        ("PUT", "/api/v1/admin/brands/1"),
        ("DELETE", "/api/v1/admin/brands/1"),
        ("GET", "/api/v1/admin/attributes"),
        ("POST", "/api/v1/admin/attributes/1/options"),
        ("GET", "/api/v1/admin/products"),
        ("GET", "/api/v1/admin/products/1"),
        ("POST", "/api/v1/admin/products"),
        ("PUT", "/api/v1/admin/products/1"),
        ("POST", "/api/v1/admin/products/1/variants"),
        ("PUT", "/api/v1/admin/variants/1"),
        ("PUT", "/api/v1/admin/products/1/attributes"),
        ("POST", "/api/v1/admin/products/1/images"),
        ("PUT", "/api/v1/admin/products/1/images/order"),
        ("DELETE", "/api/v1/admin/images/1"),
    ];

    public static TheoryData<string, string> Endpoints => ToTheoryData(All);

    public static TheoryData<string, string> WriteEndpoints => ToTheoryData(All.Where(e => e.Method != "GET"));

    private static TheoryData<string, string> ToTheoryData(IEnumerable<(string Method, string Url)> endpoints)
    {
        var data = new TheoryData<string, string>();
        foreach (var (method, url) in endpoints)
        {
            data.Add(method, url);
        }

        return data;
    }

    private static HttpRequestMessage Request(string method, string url, string? csrfToken)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method != "GET")
        {
            request.Content = url.EndsWith("/images", StringComparison.Ordinal)
                ? new MultipartFormDataContent { { new ByteArrayContent([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]), "file", "a.png" } }
                : JsonContent.Create(new { }, options: Json);
        }

        if (csrfToken is not null)
        {
            request.Headers.Add("X-XSRF-TOKEN", csrfToken);
        }

        return request;
    }

    private static async Task<string> CsrfAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<CsrfBody>("/api/v1/auth/csrf", Json))!.Token;

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Anonymous_Gets401(string method, string url)
    {
        var client = NewClient();

        // With a valid CSRF token, so only the missing authentication can be what rejects the call.
        var response = await client.SendAsync(Request(method, url, await CsrfAsync(client)));

        await AssertProblemAsync(response, 401);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Customer_Gets403(string method, string url)
    {
        var (customer, _, _) = await RegisterCustomerAsync();

        var response = await customer.SendAsync(Request(method, url, await CsrfAsync(customer)));

        await AssertProblemAsync(response, 403);
    }

    [Theory]
    [MemberData(nameof(WriteEndpoints))]
    public async Task AdminWrite_WithoutCsrfToken_IsRejected(string method, string url)
    {
        var admin = await LoginAdminAsync();

        var response = await admin.SendAsync(Request(method, url, csrfToken: null));

        await AssertProblemAsync(response, 400, "antiforgery_invalid");
    }

    [Fact]
    public async Task AdminWrite_WithCsrfTokenFromAnotherSession_IsRejected_AndNothingIsCreated()
    {
        var admin = await LoginAdminAsync();
        var foreignToken = await CsrfAsync(await LoginAdminAsync());
        var slug = Unique("csrf");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/brands")
        {
            Content = JsonContent.Create(new { name = "برند " + slug, slug, isActive = true }, options: Json),
        };
        request.Headers.Add("X-XSRF-TOKEN", foreignToken);

        await AssertProblemAsync(await admin.SendAsync(request), 400, "antiforgery_invalid");
        var brands = await GetOkAsync<IdSlugBody[]>(admin, "/api/v1/admin/brands");
        Assert.DoesNotContain(brands, b => b.Slug == slug);
    }

    public sealed record IdSlugBody(int Id, string Slug);
}
