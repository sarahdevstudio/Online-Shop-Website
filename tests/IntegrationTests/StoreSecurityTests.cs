using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Shop.Api.Infrastructure;

namespace Shop.IntegrationTests;

/// <summary>Client-IP handling (spoofed X-Forwarded-For), the per-IP order rate limit, and keeping tokens out of logs.</summary>
public sealed class StoreSecurityTests(ApiFixture fixture) : StoreTestBase(fixture)
{
    private const string Loopback = "127.0.0.1"; // trusted by default: stands in for the storefront server

    private static async Task<HttpResponseMessage> PostOrderAsync(HttpClient client, string peer, string? forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/store/orders")
        {
            Content = JsonContent.Create(Checkout(NewMobile(), "cashOnDelivery", 0)),
        };
        request.Headers.Add("X-Storefront-Key", ApiFixture.StorefrontKey);
        request.Headers.Add(TestPeerStartupFilter.Header, peer);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        if (forwardedFor is not null)
        {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }

        return await client.SendAsync(request);
    }

    /// <summary>A second host on the same database with a small order rate limit (3 per window).</summary>
    private WebApplicationFactory<Program> LimitedHost() =>
        Fixture.Factory.WithWebHostBuilder(host => host.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Checkout:OrderPermitLimit"] = "3" })));

    private static async Task<HttpStatusCode[]> StatusesAsync(HttpClient client, int count, Func<int, (string Peer, string? Forwarded)> source)
    {
        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < count; i++)
        {
            var (peer, forwarded) = source(i);
            codes.Add((await PostOrderAsync(client, peer, forwarded)).StatusCode);
        }

        return codes.ToArray();
    }

    [Fact]
    public async Task SpoofedForwardedFor_FromAnUntrustedPeer_DoesNotChangeTheRateLimitKey()
    {
        await using var host = LimitedHost();
        var client = host.CreateClient();
        var peer = "10.77.0.1";

        // The browser (an untrusted peer) rotates X-Forwarded-For on every request; it is still one bucket.
        var codes = await StatusesAsync(client, 5, i => (peer, $"203.0.113.{i + 1}"));

        Assert.Equal(
            [HttpStatusCode.Conflict, HttpStatusCode.Conflict, HttpStatusCode.Conflict, HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests],
            codes);
        // A different real peer has its own bucket.
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await PostOrderAsync(client, "10.77.0.2", null)).StatusCode);
    }

    [Fact]
    public async Task ForwardedFor_FromTheTrustedStorefront_IsTheKey_AndOnlyItsEntryCounts()
    {
        await using var host = LimitedHost();
        var client = host.CreateClient();

        // Different forwarded clients behind the trusted storefront get separate buckets...
        var distinct = await StatusesAsync(client, 5, i => (Loopback, $"198.51.100.{i + 1}"));
        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, distinct);

        // ...one forwarded client is limited...
        var same = await StatusesAsync(client, 4, _ => (Loopback, "198.51.100.200"));
        Assert.Equal(HttpStatusCode.TooManyRequests, same[3]);

        // ...and extra entries a browser smuggled in on the left of the chain are ignored (only the rightmost counts).
        var smuggled = await StatusesAsync(client, 4, i => (Loopback, $"192.0.2.{i + 1}, 198.51.100.201"));
        Assert.Equal(HttpStatusCode.TooManyRequests, smuggled[3]);
    }

    [Fact]
    public async Task StoredClientIp_IsThePeer_UnlessTheTrustedStorefrontForwardsOne()
    {
        var admin = await LoginAdminAsync();
        var variants = await CreateVariantsAsync(admin, (100_000, 5), (100_000, 5));

        async Task<string> OrderFromAsync(int variant, string peer, string? forwardedFor)
        {
            var client = Fixture.Factory.CreateClient();
            var shopper = new Shopper(client, peer);
            await shopper.AddAsync(variant, 1);
            var total = (await shopper.CartAsync()).Total;
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/store/orders")
            {
                Content = JsonContent.Create(Checkout(NewMobile(), "cashOnDelivery", total)),
            };
            request.Headers.Add("X-Storefront-Key", ApiFixture.StorefrontKey);
            request.Headers.Add("X-Cart-Token", shopper.CartToken!);
            request.Headers.Add(TestPeerStartupFilter.Header, peer);
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            if (forwardedFor is not null)
            {
                request.Headers.Add("X-Forwarded-For", forwardedFor);
            }

            var response = await client.SendAsync(request);
            var placed = await Shopper.ReadAsync<PlacedBody>(response);
            return (await AdminOrderAsync(admin, await OrderIdAsync(placed.Number))).ClientIp;
        }

        Assert.Equal("10.88.0.5", await OrderFromAsync(variants[0], "10.88.0.5", forwardedFor: "1.2.3.4"));
        Assert.Equal("1.2.3.4", await OrderFromAsync(variants[1], Loopback, forwardedFor: "1.2.3.4"));
    }

    [Fact]
    public async Task OrderToken_NeverAppearsInLogs()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 100_000, 5);
        var shopper = NewShopper();
        await shopper.AddAsync(variant, 1);
        var cartToken = shopper.CartToken!;
        var placed = await PlaceOkAsync(shopper, method: "cardToCard");

        await GetOrderAsync(shopper, placed.AccessToken);
        await shopper.SendAsync(HttpMethod.Post, $"/api/v1/store/orders/{placed.AccessToken}/transfer-reference",
            new { tracking = "12345678", time = "now", cardLast4 = "1234" });
        await shopper.SendAsync(HttpMethod.Post, $"/api/v1/store/orders/{placed.AccessToken}/transfer-reference", new { tracking = "1" }); // invalid → 400 path logged too
        await shopper.SendAsync(HttpMethod.Get, "/api/v1/store/orders/" + new string('B', 43)); // 404 path logged too

        var lines = Fixture.LogSink.Lines;
        Assert.True(
            lines.Any(l => l.Contains("/api/v1/store/orders/***", StringComparison.Ordinal)),
            "request log lines: " + string.Join(" | ", lines.Where(l => l.Contains("responded", StringComparison.Ordinal)).TakeLast(8)));
        Assert.DoesNotContain(lines, l => l.Contains(placed.AccessToken, StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.Contains(cartToken, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("/api/v1/store/orders/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "/api/v1/store/orders/***")]
    [InlineData("/api/v1/store/orders/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA/transfer-reference", "/api/v1/store/orders/***/transfer-reference")]
    [InlineData("/order/AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", "/order/***")]
    [InlineData("/api/v1/store/cart", "/api/v1/store/cart")]
    [InlineData("/api/v1/admin/orders/42", "/api/v1/admin/orders/42")]
    public void LogPaths_MaskOrderTokens(string path, string expected) => Assert.Equal(expected, LogPaths.Mask(path));
}
