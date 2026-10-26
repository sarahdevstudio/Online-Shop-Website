using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Shop.IntegrationTests;

public sealed record AccountBody(Guid Id, string Username, string? Email, string DisplayName, string[] Roles);

public sealed record ProfileBody(Guid CustomerId, string Email, string FullName, string? Phone);

public sealed record ProblemBody(int Status, string? Title, string? Detail, string? Code, string? TraceId, Dictionary<string, string[]>? Errors);

public sealed record CsrfBody(string HeaderName, string Token);

[Collection(ApiCollection.Name)]
public abstract class ApiTestBase(ApiFixture fixture)
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    protected ApiFixture Fixture { get; } = fixture;

    /// <summary>A client with its own cookie jar, i.e. its own browser session.</summary>
    protected HttpClient NewClient() =>
        Fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    /// <summary>POST JSON with a fresh antiforgery token (tokens are bound to the current identity).</summary>
    protected static async Task<HttpResponseMessage> PostAsync(HttpClient client, string url, object? body = null)
    {
        var csrf = await client.GetFromJsonAsync<CsrfBody>("/api/v1/auth/csrf", Json);
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        request.Headers.Add(csrf!.HeaderName, csrf.Token);
        return await client.SendAsync(request);
    }

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(Json);
        return value ?? throw new InvalidOperationException("Response body was empty.");
    }

    protected static async Task<ProblemBody> AssertProblemAsync(HttpResponseMessage response, int status, string? code = null)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await ReadAsync<ProblemBody>(response);
        Assert.Equal(status, problem.Status);
        Assert.False(string.IsNullOrEmpty(problem.TraceId));
        if (code is not null)
        {
            Assert.Equal(code, problem.Code);
        }

        return problem;
    }

    protected async Task<(HttpClient Client, string Email, string Password)> RegisterCustomerAsync()
    {
        var client = NewClient();
        var email = TestData.NewEmail();
        var password = TestData.NewPassword();
        var response = await PostAsync(client, "/api/v1/auth/register", new { email, password, fullName = "Test Customer", phone = "09120000000" });
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);
        return (client, email, password);
    }

    protected async Task<HttpClient> LoginAdminAsync()
    {
        var client = NewClient();
        var response = await PostAsync(client, "/api/v1/auth/login", new { username = ApiFixture.AdminEmail, password = Fixture.AdminPassword });
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        return client;
    }
}
