using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;

namespace Shop.IntegrationTests;

public sealed record CartLineBody(
    int VariantId, string ProductName, string? VariantLabel, string Sku, long UnitPrice, int Quantity, int MaxQuantity,
    long LineTotal, bool Available, string? Notice, long? PreviousPrice);

public sealed record CartBody(
    CartLineBody[] Lines, int ItemCount, long Subtotal, long ShippingFee, long Total, long? FreeShippingThreshold, bool HasBlockingIssue);

public sealed record CartMutationBody(string? NewToken, CartBody Cart, bool Clamped);

public sealed record PlacedBody(string AccessToken, int Number, bool Replayed);

public sealed record OrderLineBody(string ProductName, string? VariantLabel, string Sku, long UnitPrice, int Quantity, long LineTotal);

public sealed record PaymentInstructionsBody(string CardNumber, string HolderName, string BankName, long Amount);

public sealed record TransferReferenceBody(string Tracking, string Time, string CardLast4);

public sealed record OrderBody(
    int Number, string Status, string PaymentStatus, string PaymentMethod, string FullName, string Mobile, string Province, string City,
    string Address, string PostalCode, string? Note, OrderLineBody[] Lines, long Subtotal, long ShippingFee, long Total,
    PaymentInstructionsBody? PaymentInstructions, TransferReferenceBody? TransferReference);

public sealed record StatusLogBody(string? From, string To, Guid? AdminUserId);

public sealed record AdminOrderBody(int Id, OrderBody Order, string ClientIp, StatusLogBody[] StatusLog);

public sealed record AdminOrderListBody(AdminOrderListItemBody[] Items, int Total);

public sealed record AdminOrderListItemBody(int Id, int Number, string Status, string PaymentStatus, string PaymentMethod, string Mobile, long Total, bool HasTransferReference);

public sealed record StockLineBody(int VariantId, string ProductName, string? VariantLabel, int Requested, int Available);

public sealed record StockProblemBody(int Status, string? Code, StockLineBody[]? Lines, long? Total, long? ExpectedTotal, string? Detail);

/// <summary>A browser as the Next.js server sees it: a cart token, a client address, and the shared storefront key.</summary>
public sealed class Shopper(HttpClient client, string ip)
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase) },
    };

    public HttpClient Client { get; } = client;
    public string Ip { get; } = ip;
    public string? CartToken { get; set; }

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, object? body = null, Guid? idempotencyKey = null, string? cartToken = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-Storefront-Key", ApiFixture.StorefrontKey);
        request.Headers.Add(TestPeerStartupFilter.Header, Ip);
        if ((cartToken ?? CartToken) is { } token)
        {
            request.Headers.Add("X-Cart-Token", token);
        }

        if (idempotencyKey is { } key)
        {
            request.Headers.Add("Idempotency-Key", key.ToString());
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        return await Client.SendAsync(request);
    }

    public async Task<HttpResponseMessage> AddAsync(int variantId, int quantity)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/v1/store/cart/items", new { variantId, quantity });
        if (response.IsSuccessStatusCode)
        {
            // ReadAsString buffers the content, so the test can read the same response again.
            using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            // What the storefront does with its cookie: a new token replaces the old one (e.g. after an order emptied the cart).
            if (json.RootElement.TryGetProperty("newToken", out var newToken) && newToken.ValueKind == System.Text.Json.JsonValueKind.String)
            {
                CartToken = newToken.GetString();
            }
        }

        return response;
    }

    public async Task<CartBody> CartAsync()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/v1/store/cart");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CartBody>(Json))!;
    }

    public static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;
}

public abstract class StoreTestBase(ApiFixture fixture) : CatalogTestBase(fixture)
{
    private static int _ipCounter = 1000;

    protected static string NewMobile() => "09" + Random.Shared.NextInt64(100_000_000, 999_999_999);

    /// <summary>Every shopper gets its own address (outside the trusted loopback range), so tests never share IP limits.</summary>
    protected Shopper NewShopper()
    {
        var n = Interlocked.Increment(ref _ipCounter);
        return new Shopper(Fixture.Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false }),
            $"10.{n / 65536 % 256}.{n / 256 % 256}.{n % 256}");
    }

    /// <summary>A published product with one variant per (price, stock) pair; returns the variant ids.</summary>
    protected async Task<int[]> CreateVariantsAsync(HttpClient admin, params (long Price, int Stock)[] variants)
    {
        var category = await CreateCategoryAsync(admin, Unique("c"));
        var product = await CreateProductAsync(admin, category.Id, Unique("p"), variants: variants);
        return product.Variants.OrderBy(v => v.SortOrder).Select(v => v.Id).ToArray();
    }

    protected async Task<int> CreateVariantAsync(HttpClient admin, long price, int stock) =>
        (await CreateVariantsAsync(admin, (price, stock)))[0];

    protected Task<int> StockOfAsync(int variantId) =>
        Fixture.WithDbAsync(db => db.ProductVariants.Where(v => v.Id == variantId).Select(v => v.Stock).SingleAsync());

    protected Task SetStockAsync(int variantId, int stock) =>
        Fixture.WithDbAsync(async db => await db.ProductVariants.Where(v => v.Id == variantId)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.Stock, stock)));

    protected Task SetPriceAsync(int variantId, long price) =>
        Fixture.WithDbAsync(async db => await db.ProductVariants.Where(v => v.Id == variantId)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.Price, price).SetProperty(v => v.CompareAtPrice, (long?)null)));

    protected static object Checkout(string mobile, string method, long expectedTotal, string? note = null) => new
    {
        fullName = "علی رضایی",
        mobile,
        province = "تهران",
        city = "تهران",
        address = "خیابان ولیعصر، کوچه گل‌ها، پلاک ۱۲",
        postalCode = "1234567890",
        note,
        paymentMethod = method,
        expectedTotal,
    };

    /// <summary>Adds the lines, reads the cart total and places an order for it. Returns the response and the key used.</summary>
    protected static async Task<(HttpResponseMessage Response, Guid Key)> PlaceAsync(
        Shopper shopper, string? mobile = null, string method = "cashOnDelivery", long? expectedTotal = null, Guid? key = null)
    {
        var total = expectedTotal ?? (await shopper.CartAsync()).Total;
        var k = key ?? Guid.NewGuid();
        var response = await shopper.SendAsync(HttpMethod.Post, "/api/v1/store/orders", Checkout(mobile ?? NewMobile(), method, total), k);
        return (response, k);
    }

    protected static async Task<PlacedBody> PlaceOkAsync(Shopper shopper, string? mobile = null, string method = "cashOnDelivery")
    {
        var (response, _) = await PlaceAsync(shopper, mobile, method);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await Shopper.ReadAsync<PlacedBody>(response);
    }

    protected static async Task<OrderBody> GetOrderAsync(Shopper shopper, string token)
    {
        var response = await shopper.SendAsync(HttpMethod.Get, $"/api/v1/store/orders/{token}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await Shopper.ReadAsync<OrderBody>(response);
    }

    protected static async Task<AdminOrderBody> AdminOrderAsync(HttpClient admin, int orderId) =>
        await GetOkAsync<AdminOrderBody>(admin, $"/api/v1/admin/orders/{orderId}");

    protected async Task<int> OrderIdAsync(int number) =>
        await Fixture.WithDbAsync(db => db.Orders.Where(o => o.Number == number).Select(o => o.Id).SingleAsync());
}
