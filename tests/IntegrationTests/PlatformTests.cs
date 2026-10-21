using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shop.Application.Accounts;
using Shop.Application.Common;

namespace Shop.IntegrationTests;

public sealed class PlatformTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_Endpoints_ReturnHealthy(string url)
    {
        var response = await NewClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OpenApiDocument_DescribesTheAuthEndpoints()
    {
        var response = await NewClient().GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/api/v1/auth/login", out _));
        Assert.True(paths.TryGetProperty("/api/v1/auth/register", out _));
    }

    [Fact]
    public async Task Responses_CarryBasicSecurityHeaders()
    {
        var response = await NewClient().GetAsync("/health/live");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
    }

    [Fact]
    public async Task UnexpectedException_ReturnsGenericProblem_WithoutLeakingDetails()
    {
        const string secret = "SECRET-INTERNAL-DETAIL";
        using var factory = Fixture.Factory.WithWebHostBuilder(host =>
            host.ConfigureTestServices(services => services.AddScoped<IAccountService>(_ => new ExplodingAccountService(secret))));
        var client = factory.CreateClient();

        var response = await PostAsync(client, "/api/v1/auth/login", new { email = TestData.NewEmail(), password = TestData.NewPassword() });

        await AssertProblemAsync(response, 500);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(secret, body, StringComparison.Ordinal);
        Assert.DoesNotContain("at Shop.", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthEndpoints_AreRateLimited()
    {
        using var factory = Fixture.Factory.WithWebHostBuilder(host =>
        {
            host.UseSetting("RateLimiting:AuthPermitLimit", "3");
            host.UseSetting("RateLimiting:WindowSeconds", "600");
        });
        var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
        {
            var response = await PostAsync(client, "/api/v1/auth/login", new { email = TestData.NewEmail(), password = TestData.NewPassword() });
            statuses.Add(response.StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
        Assert.Equal(HttpStatusCode.Unauthorized, statuses[0]);
    }

    private sealed class ExplodingAccountService(string secret) : IAccountService
    {
        public Task<Result<AccountDto>> RegisterCustomerAsync(RegisterCustomerRequest request, CancellationToken ct) => throw Boom();
        public Task<Result<AccountDto>> LoginAsync(LoginRequest request, CancellationToken ct) => throw Boom();
        public Task LogoutAsync(CancellationToken ct) => throw Boom();
        public Task<Result<AccountDto>> GetAccountAsync(Guid userId, CancellationToken ct) => throw Boom();
        public Task<Result<CustomerProfileDto>> GetCustomerProfileAsync(Guid userId, CancellationToken ct) => throw Boom();

        private InvalidOperationException Boom() => new(secret);
    }
}
