using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Shop.Api.Commands;
using Shop.Api.Infrastructure;
using Shop.Infrastructure.Persistence;

namespace Shop.IntegrationTests;

/// <summary>SITE_ENV (who may be indexed), the request log's status, and create-admin in a real Development host.</summary>
public sealed class ProductionReadinessTests(ApiFixture fixture) : CatalogTestBase(fixture)
{
    private HttpClient HostWith(string? siteEnv, out WebApplicationFactory<Program> host)
    {
        host = Fixture.Factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["SITE_ENV"] = siteEnv })));
        return host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
    }

    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Shop";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static IConfiguration Config(string? siteEnv) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["SITE_ENV"] = siteEnv }).Build();

    // ---- SITE_ENV resolution ----

    [Theory]
    [InlineData("production", "Production", "production", true)]
    [InlineData("staging", "Production", "staging", false)] // a staging deployment of the production build is not indexable
    [InlineData("development", "Production", "development", false)]
    [InlineData("PRODUCTION", "Development", "production", true)]
    [InlineData(" Staging ", "Production", "staging", false)]
    [InlineData(null, "Production", "production", true)]
    [InlineData("", "Production", "production", true)]
    [InlineData(null, "Development", "development", false)]
    [InlineData(null, "Staging", "development", false)]
    [InlineData(null, "Testing", "development", false)]
    [InlineData("prod", "Production", "development", false)] // a typo must never make a site indexable
    [InlineData("yes", "Production", "development", false)]
    public void SiteEnv_ResolvesSafely(string? siteEnv, string aspNetEnvironment, string expected, bool indexable)
    {
        var environment = new FakeEnvironment(aspNetEnvironment);

        Assert.Equal(expected, SiteEnvironment.Resolve(Config(siteEnv), environment));
        Assert.Equal(indexable, SiteEnvironment.IsIndexable(Config(siteEnv), environment));
    }

    // ---- API: header and robots.txt ----

    [Theory]
    [InlineData("staging")]
    [InlineData("development")]
    [InlineData(null)] // the test host is not Production: noindex by default
    [InlineData("typo")]
    public async Task NonProduction_EveryApiResponse_SaysNoindex(string? siteEnv)
    {
        var client = HostWith(siteEnv, out var host);
        await using var _ = host;

        foreach (var path in new[] { "/health/live", "/api/v1/catalog/categories", "/api/v1/catalog/pages", "/api/v1/auth/me", "/api/v1/no-such-endpoint", "/robots.txt", "/media/none.jpg" })
        {
            var response = await client.GetAsync(path);
            Assert.True(response.Headers.TryGetValues("X-Robots-Tag", out var values), $"{path} → {(int)response.StatusCode} has no X-Robots-Tag");
            Assert.Equal("noindex, nofollow", string.Join(",", values!));
        }

        // errors too: an unauthenticated admin call (401) and a malformed request
        Assert.Contains("noindex", (await client.GetAsync("/api/v1/admin/orders")).Headers.GetValues("X-Robots-Tag").Single());
    }

    [Fact]
    public async Task ProductionMode_SendsNoNoindexHeader()
    {
        var client = HostWith("production", out var host);
        await using var _ = host;

        foreach (var path in new[] { "/health/live", "/api/v1/catalog/categories", "/api/v1/no-such-endpoint", "/robots.txt" })
        {
            var response = await client.GetAsync(path);
            Assert.False(response.Headers.Contains("X-Robots-Tag"), $"{path} should be indexable-by-header in production");
        }
    }

    [Theory]
    [InlineData("production")]
    [InlineData("staging")]
    [InlineData(null)]
    public async Task ApiRobotsTxt_IsAlwaysDisallowAll(string? siteEnv)
    {
        var client = HostWith(siteEnv, out var host);
        await using var _ = host;

        var response = await client.GetAsync("/robots.txt");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("User-agent: *", body);
        Assert.Contains("Disallow: /", body);
        Assert.DoesNotContain("Allow:", body);
    }

    // ---- request log shows the real status ----

    [Fact]
    public async Task StaleRowVersion_IsLoggedAs409_NotAs500()
    {
        var admin = await LoginAdminAsync();
        var category = await CreateCategoryAsync(admin, Unique("c"));
        var product = await CreateProductAsync(admin, category.Id, Unique("p"));
        var variant = product.Variants[0];

        var response = await SendAsync(admin, HttpMethod.Put, $"/api/v1/admin/variants/{variant.Id}",
            new { sku = variant.Sku, price = variant.Price + 1, compareAtPrice = (long?)null, stock = variant.Stock, isActive = true, sortOrder = 0, rowVersion = new byte[8] });
        await AssertProblemAsync(response, 409);

        var marker = $"/api/v1/admin/variants/{variant.Id}\" responded"; // the path is quoted in the log line
        string[] lines = [];
        for (var i = 0; i < 20 && lines.Length == 0; i++)
        {
            lines = Fixture.LogSink.Lines.Where(l => l.Contains(marker, StringComparison.Ordinal)).ToArray();
            if (lines.Length == 0)
            {
                await Task.Delay(100);
            }
        }

        Assert.Single(lines);
        Assert.Contains("responded 409", lines[0]);
        Assert.DoesNotContain("responded 500", lines[0]);
    }

    // ---- create-admin in a real Development host ----

    private sealed class EnvScope : IDisposable
    {
        private readonly Dictionary<string, string?> _previous = [];

        public EnvScope(params (string Name, string? Value)[] variables)
        {
            foreach (var (name, value) in variables)
            {
                _previous[name] = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, value);
            }
        }

        public void Dispose()
        {
            foreach (var (name, value) in _previous)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }

    [Fact]
    public async Task CreateAdmin_RunsInADevelopmentHost_AgainstARealDatabase()
    {
        // Development validates the whole service graph when the host is built: this is the run that once failed.
        var connection = ApiFixture.NewDatabaseConnectionString();
        var email = TestData.NewEmail();
        var password = TestData.NewPassword();
        var options = new DbContextOptionsBuilder<ShopDbContext>().UseSqlServer(connection).Options;
        try
        {
            using (new EnvScope(
                ("ASPNETCORE_ENVIRONMENT", "Development"),
                ("ConnectionStrings__Default", connection),
                ("Database__MigrateOnStartup", "true"),
                ("Seed__AdminEmail", null),
                ("Seed__AdminPassword", null),
                (CreateAdminCommand.PasswordVariable, password)))
            {
                Assert.Equal(0, await CreateAdminCommand.RunAsync(["--email", email]));
                Assert.Equal(1, await CreateAdminCommand.RunAsync(["--email", email])); // already exists: refused, exit 1
                Assert.Equal(2, await CreateAdminCommand.RunAsync(["--email", TestData.NewEmail(), "--password", password])); // never an argument
            }

            await using var db = new ShopDbContext(options);
            var roles = await (
                    from u in db.Users
                    where u.Email == email
                    join ur in db.UserRoles on u.Id equals ur.UserId
                    join r in db.Roles on ur.RoleId equals r.Id
                    select r.Name)
                .ToListAsync();
            Assert.Equal(["Admin"], roles);
            Assert.True(await db.StoreSettings.AnyAsync()); // the same startup work as the API: roles, settings, draft pages
            Assert.Equal(5, await db.ContentPages.CountAsync());
        }
        finally
        {
            Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
            await using var cleanup = new ShopDbContext(options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task CreateAdmin_RefusesAWeakPassword_InADevelopmentHost()
    {
        var connection = ApiFixture.NewDatabaseConnectionString();
        var options = new DbContextOptionsBuilder<ShopDbContext>().UseSqlServer(connection).Options;
        try
        {
            using (new EnvScope(
                ("ASPNETCORE_ENVIRONMENT", "Development"),
                ("ConnectionStrings__Default", connection),
                ("Database__MigrateOnStartup", "true"),
                ("Seed__AdminEmail", null),
                ("Seed__AdminPassword", null),
                (CreateAdminCommand.PasswordVariable, "short-pw-1")))
            {
                Assert.Equal(2, await CreateAdminCommand.RunAsync(["--email", TestData.NewEmail()]));
            }
        }
        finally
        {
            Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
            await using var cleanup = new ShopDbContext(options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }
}
