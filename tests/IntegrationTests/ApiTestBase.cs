using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Shop.IntegrationTests;

public sealed record AccountBody(Guid Id, string Username, string? Email, string DisplayName, string[] Roles);

public sealed record ProfileBody(Guid CustomerId, string Email, string FullName, string? Phone);

/// <summary>The error side of <c>ResponseModel&lt;T&gt;</c>, plus the status code the test asserted against.</summary>
public sealed record ProblemBody(int Status, string? ErrorMessage, string? ErrorCode, Dictionary<string, string[]>? ValidationErrors)
{
    // Kept as a thin alias of ErrorMessage: every response has exactly one error string now, not a separate
    // title/detail pair.
    public string? Title => ErrorMessage;
    public string? Detail => ErrorMessage;
    public string? Code => ErrorCode;
    public Dictionary<string, string[]>? Errors => ValidationErrors;
}

public sealed record CsrfBody(string HeaderName, string Token);

/// <summary>Mirrors <c>Shop.Api.Infrastructure.ResponseModel&lt;T&gt;</c> on the wire.</summary>
internal sealed record Envelope<T>(T? Result, string? ErrorMessage, string? ErrorCode, Dictionary<string, string[]>? ValidationErrors, bool IsSuccess);

[Collection(ApiCollection.Name)]
public abstract class ApiTestBase(ApiFixture fixture)
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    protected ApiFixture Fixture { get; } = fixture;

    /// <summary>A client with its own cookie jar, i.e. its own browser session.</summary>
    protected HttpClient NewClient() =>
        Fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    protected static async Task<CsrfBody> CsrfTokenAsync(HttpClient client)
    {
        var envelope = await client.GetFromJsonAsync<Envelope<CsrfBody>>("/api/v1/auth/csrf", Json);
        return envelope!.Result ?? throw new InvalidOperationException("csrf result was empty.");
    }

    /// <summary>POST JSON with a fresh antiforgery token (tokens are bound to the current identity).</summary>
    protected static async Task<HttpResponseMessage> PostAsync(HttpClient client, string url, object? body = null)
    {
        var csrf = await CsrfTokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        request.Headers.Add(csrf.HeaderName, csrf.Token);
        return await client.SendAsync(request);
    }

    /// <summary>Reads the envelope and returns its <c>result</c> (throws if the body was empty or an error).</summary>
    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<Envelope<T>>(Json);
        if (envelope is null)
        {
            throw new InvalidOperationException("Response body was empty.");
        }

        return envelope.Result ?? throw new InvalidOperationException(envelope.ErrorMessage ?? "Response body was empty.");
    }

    protected static async Task<ProblemBody> AssertProblemAsync(HttpResponseMessage response, int status, string? code = null)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var envelope = await response.Content.ReadFromJsonAsync<Envelope<object>>(Json);
        Assert.NotNull(envelope);
        Assert.False(envelope!.IsSuccess);
        if (code is not null)
        {
            Assert.Equal(code, envelope.ErrorCode);
        }

        return new ProblemBody(status, envelope.ErrorMessage, envelope.ErrorCode, envelope.ValidationErrors);
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
