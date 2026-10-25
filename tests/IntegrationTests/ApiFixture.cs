using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Serilog.Core;
using Serilog.Events;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Shop.Infrastructure.Persistence;

namespace Shop.IntegrationTests;

/// <summary>
/// Hosts the real API against a real SQL Server database that exists only for this test run.
/// Point SHOP_TEST_SQL_CONNECTION at any SQL Server (database name is ignored); defaults to LocalDB.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public const string AdminEmail = "admin@shop.test";
    public const string TestCard = "6037991234567893"; // passes the Luhn check
    public const string StorefrontKey = "test-storefront-key-0123456789abcdef-xyz";

    private const string DefaultConnection =
        "Server=(localdb)\\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True";

    private string _connectionString = string.Empty;

    public string AdminPassword { get; } = TestData.NewPassword();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public CapturingLogSink LogSink { get; } = new();

    public string MediaRoot { get; } = Path.Combine(Path.GetTempPath(), $"shop-media-{Guid.NewGuid():N}");

    /// <summary>Connection string for a new, not yet created test database on the test SQL Server.</summary>
    public static string NewDatabaseConnectionString()
    {
        var baseConnection = Environment.GetEnvironmentVariable("SHOP_TEST_SQL_CONNECTION") ?? DefaultConnection;
        return new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = $"ShopMvp_Test_{Guid.NewGuid():N}" }.ConnectionString;
    }

    public Task InitializeAsync()
    {
        _connectionString = NewDatabaseConnectionString();

        // Environment variables are visible to Program before the host is built, unlike late config overrides.
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _connectionString);
        Environment.SetEnvironmentVariable("Database__MigrateOnStartup", "true");
        Environment.SetEnvironmentVariable("Auth__RequireSecureCookie", "false");
        Environment.SetEnvironmentVariable("RateLimiting__AuthPermitLimit", "100000");
        Environment.SetEnvironmentVariable("Seed__AdminEmail", AdminEmail);
        Environment.SetEnvironmentVariable("Seed__AdminPassword", AdminPassword);
        Environment.SetEnvironmentVariable("Media__RootPath", MediaRoot);
        Environment.SetEnvironmentVariable("Storefront__ApiKey", StorefrontKey);
        // First-start store settings: a valid card, so card-to-card is offered.
        Environment.SetEnvironmentVariable("StoreDefaults__CardNumber", TestCard);
        Environment.SetEnvironmentVariable("StoreDefaults__CardHolderName", "رضا احمدی");
        Environment.SetEnvironmentVariable("StoreDefaults__BankName", "ملی");
        // Tests share one host and reuse the same pseudo-addresses, so the order rate limit is only exercised on its own host.
        Environment.SetEnvironmentVariable("Checkout__OrderPermitLimit", "100000");

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host => host.UseEnvironment("Testing").ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter, TestPeerStartupFilter>();
            services.AddSingleton<Serilog.Core.ILogEventSink>(LogSink);
        }));
        using var warmup = Factory.CreateClient(); // starts the host: migrates and seeds
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        SqlConnection.ClearAllPools();

        var options = new DbContextOptionsBuilder<ShopDbContext>().UseSqlServer(_connectionString).Options;
        await using var db = new ShopDbContext(options);
        await db.Database.EnsureDeletedAsync();

        if (Directory.Exists(MediaRoot))
        {
            Directory.Delete(MediaRoot, recursive: true);
        }
    }

    public async Task<T> WithDbAsync<T>(Func<ShopDbContext, Task<T>> action)
    {
        using var scope = Factory.Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ShopDbContext>());
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "Api";
}

internal static class TestData
{
    public static string NewPassword() => "Pw-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12));

    public static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";
}

/// <summary>
/// TestServer has no socket peer. A test sets one with the X-Test-Peer-Ip header, applied before the API's own
/// middleware (so UseForwardedHeaders sees it exactly as it would see a real connection).
/// </summary>
internal sealed class TestPeerStartupFilter : IStartupFilter
{
    public const string Header = "X-Test-Peer-Ip";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            if (System.Net.IPAddress.TryParse(context.Request.Headers[Header].ToString(), out var peer))
            {
                context.Connection.RemoteIpAddress = peer;
            }

            await nextMiddleware();
        });
        next(app);
    };
}

public sealed class CapturingLogSink : ILogEventSink
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<string> _lines = new();

    public IReadOnlyCollection<string> Lines => _lines.ToArray();

    public void Emit(LogEvent logEvent) => _lines.Enqueue(logEvent.RenderMessage() + " " + logEvent.Exception);
}
