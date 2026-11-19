using System.Net;

namespace Shop.IntegrationTests;

public sealed record AdminDiscountBody(
    int Id, string Code, string Type, long Value, long? MinSubtotal, int? UsageLimit, int UsedCount, int PerMobileLimit,
    DateTime? ExpiresAtUtc, bool IsActive, DateTime CreatedAtUtc, byte[] RowVersion);

public sealed record DiscountAuditEntryBody(DateTime AtUtc, string Action, string? AdminUsername);

/// <summary>Admin CRUD, cart/checkout application, usage-limit and per-mobile-limit enforcement, and the release-on-cancel
/// rule for discount codes (F3, docs/21).</summary>
public sealed class DiscountTests(ApiFixture fixture) : StoreTestBase(fixture)
{
    private const string Admin = "/api/v1/admin/discounts";

    private static Task<HttpResponseMessage> CreateAsync(
        HttpClient admin, string code, string type = "percent", long value = 10, long? minSubtotal = null,
        int? usageLimit = null, int perMobileLimit = 1, DateTime? expiresAtUtc = null, bool isActive = true) =>
        SendAsync(admin, HttpMethod.Post, Admin, new { code, type, value, minSubtotal, usageLimit, perMobileLimit, expiresAtUtc, isActive });

    private static async Task<AdminDiscountBody> CreateOkAsync(
        HttpClient admin, string code, string type = "percent", long value = 10, long? minSubtotal = null,
        int? usageLimit = null, int perMobileLimit = 1, DateTime? expiresAtUtc = null, bool isActive = true)
    {
        var response = await CreateAsync(admin, code, type, value, minSubtotal, usageLimit, perMobileLimit, expiresAtUtc, isActive);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await Shopper.ReadAsync<AdminDiscountBody>(response);
    }

    [Fact]
    public async Task AdminCrud_CreateGetUpdateDelete_AStaleRowVersionIs409_AndADuplicateCodeIs409()
    {
        var admin = await LoginAdminAsync();
        var code = UniqueCode("SAVE");
        var discount = await CreateOkAsync(admin, code, "fixed", 50_000, minSubtotal: 100_000, usageLimit: 5, perMobileLimit: 2);
        Assert.Equal(code, discount.Code);
        Assert.Equal(0, discount.UsedCount);

        await AssertProblemAsync(await CreateAsync(admin, code), 409, "code_taken");

        var fetched = await GetOkAsync<AdminDiscountBody>(admin, $"{Admin}/{discount.Id}");
        Assert.Equal(discount.Code, fetched.Code);

        var updated = await SendOkAsync<AdminDiscountBody>(admin, HttpMethod.Put, $"{Admin}/{discount.Id}",
            new { type = "fixed", value = 60_000, minSubtotal = (long?)null, usageLimit = (int?)null, perMobileLimit = 3, expiresAtUtc = (DateTime?)null, isActive = true, rowVersion = discount.RowVersion });
        Assert.Equal(60_000, updated.Value);
        Assert.Null(updated.UsageLimit);

        // Stale row version: the first save above already moved it.
        await AssertProblemAsync(
            await SendAsync(admin, HttpMethod.Put, $"{Admin}/{discount.Id}",
                new { type = "fixed", value = 70_000, minSubtotal = (long?)null, usageLimit = (int?)null, perMobileLimit = 3, expiresAtUtc = (DateTime?)null, isActive = true, rowVersion = discount.RowVersion }),
            409, "concurrent_update");

        var audit = await GetOkAsync<DiscountAuditEntryBody[]>(admin, $"{Admin}/{discount.Id}/audit");
        Assert.Equal(["updated", "created"], audit.Select(a => a.Action));

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(admin, HttpMethod.Delete, $"{Admin}/{discount.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{Admin}/{discount.Id}")).StatusCode);
    }

    [Fact]
    public async Task PercentAndFixedCodes_ReduceTheCartAndOrderTotal_AndTheOrderSnapshotsTheCodeAndAmount()
    {
        var admin = await LoginAdminAsync();
        var percent = await CreateOkAsync(admin, UniqueCode("PCT"), "percent", 10);
        var variant = await CreateVariantAsync(admin, 200_000, 10);

        var shopper = NewShopper();
        await shopper.AddAsync(variant, 1);
        var before = await shopper.CartAsync();
        Assert.Equal(250_000, before.Total); // 200,000 + 50,000 shipping, no discount yet

        Assert.Equal(HttpStatusCode.OK, (await shopper.SendAsync(HttpMethod.Post, "/api/v1/store/cart/discount", new { code = percent.Code })).StatusCode);
        var withDiscount = await shopper.CartAsync();
        Assert.Equal(percent.Code, withDiscount.Discount!.Code);
        Assert.Equal(20_000, withDiscount.Discount.Amount); // 10% of 200,000
        Assert.Null(withDiscount.Discount.Reason);
        Assert.Equal(230_000, withDiscount.Total);

        var placed = await PlaceOkAsync(shopper);
        var order = await GetOrderAsync(shopper, placed.AccessToken);
        Assert.Equal(percent.Code, order.DiscountCode);
        Assert.Equal(20_000, order.DiscountAmount);
        Assert.Equal(230_000, order.Total);
    }

    [Fact]
    public async Task ACodeBelowItsMinimum_PreviewsAsUnusable_AndIsDroppedAtCheckout()
    {
        var admin = await LoginAdminAsync();
        var discount = await CreateOkAsync(admin, UniqueCode("MIN"), "fixed", 10_000, minSubtotal: 500_000);
        var variant = await CreateVariantAsync(admin, 100_000, 10);
        var shopper = NewShopper();
        await shopper.AddAsync(variant, 1);
        await shopper.SendAsync(HttpMethod.Post, "/api/v1/store/cart/discount", new { code = discount.Code });

        var cart = await shopper.CartAsync();
        Assert.Equal(0, cart.Discount!.Amount);
        Assert.Equal("api.error.discount-min-subtotal-not-met", cart.Discount.Reason);
        Assert.Equal(150_000, cart.Total); // the cart quote already excludes it, so checkout's own total matches

        var placed = await PlaceOkAsync(shopper);
        var order = await GetOrderAsync(shopper, placed.AccessToken);
        Assert.Null(order.DiscountCode);
        Assert.Equal(0, order.DiscountAmount);
    }

    [Fact]
    public async Task UnknownCode_IsRejectedWhenApplied()
    {
        var shopper = NewShopper();
        var variant = await CreateVariantAsync(await LoginAdminAsync(), 100_000, 5);
        await shopper.AddAsync(variant, 1);
        await AssertProblemAsync(
            await shopper.SendAsync(HttpMethod.Post, "/api/v1/store/cart/discount", new { code = "NOPE-NOPE" }), 404, "discount_not_found");
    }

    [Fact]
    public async Task UsageLimitExhausted_TheNextCheckoutDoesNotGetTheDiscount_AndReportsAsAPriceChange()
    {
        var admin = await LoginAdminAsync();
        var discount = await CreateOkAsync(admin, UniqueCode("ONE"), "fixed", 10_000, usageLimit: 1);
        var variant = await CreateVariantAsync(admin, 200_000, 10);

        var first = NewShopper();
        await first.AddAsync(variant, 1);
        await first.SendAsync(HttpMethod.Post, "/api/v1/store/cart/discount", new { code = discount.Code });
        await PlaceOkAsync(first);

        var second = NewShopper();
        await second.AddAsync(variant, 1);
        await second.SendAsync(HttpMethod.Post, "/api/v1/store/cart/discount", new { code = discount.Code });
        var quoted = await second.CartAsync(); // the preview still shows it as usable (usage limits are checked only at placement)
        Assert.Equal(10_000, quoted.Discount!.Amount);

        // Placing with the quoted (discounted) total: the server silently drops the exhausted code, so the total no longer matches.
        var (response, _) = await PlaceAsync(second, expectedTotal: quoted.Total);
        await AssertProblemAsync(response, 409, "price_changed");
    }

    [Fact]
    public async Task PerMobileLimitExhausted_TheSameMobilesSecondOrderDoesNotGetTheDiscount()
    {
        var admin = await LoginAdminAsync();
        var discount = await CreateOkAsync(admin, UniqueCode("MOB"), "fixed", 10_000, perMobileLimit: 1);
        var variant = await CreateVariantAsync(admin, 200_000, 10);
        var mobile = NewMobile();

        var first = NewShopper();
        await first.AddAsync(variant, 1);
        await first.SendAsync(HttpMethod.Post, "/api/v1/store/cart/discount", new { code = discount.Code });
        await PlaceOkAsync(first, mobile);

        var second = NewShopper();
        await second.AddAsync(variant, 1);
        await second.SendAsync(HttpMethod.Post, "/api/v1/store/cart/discount", new { code = discount.Code });
        var quoted = await second.CartAsync();
        var (response, _) = await PlaceAsync(second, mobile, expectedTotal: quoted.Total);
        await AssertProblemAsync(response, 409, "price_changed");

        // Without the discount in the expected total, the same mobile's second order places fine (just without it).
        var (ok, _) = await PlaceAsync(second, mobile, expectedTotal: quoted.Subtotal + quoted.ShippingFee);
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
    }

    [Fact]
    public async Task CancellingAnOrder_ReleasesItsDiscountUsage_ForTheSameMobile()
    {
        var admin = await LoginAdminAsync();
        var discount = await CreateOkAsync(admin, UniqueCode("CNL"), "fixed", 10_000, perMobileLimit: 1);
        var variant = await CreateVariantAsync(admin, 200_000, 10);
        var mobile = NewMobile();

        var first = NewShopper();
        await first.AddAsync(variant, 1);
        await first.SendAsync(HttpMethod.Post, "/api/v1/store/cart/discount", new { code = discount.Code });
        var placed = await PlaceOkAsync(first, mobile);
        var id = await OrderIdAsync(placed.Number);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(admin, HttpMethod.Post, $"/api/v1/admin/orders/{id}/cancel")).StatusCode);

        var second = NewShopper();
        await second.AddAsync(variant, 1);
        await second.SendAsync(HttpMethod.Post, "/api/v1/store/cart/discount", new { code = discount.Code });
        var placedAgain = await PlaceOkAsync(second, mobile);
        var order = await GetOrderAsync(second, placedAgain.AccessToken);
        Assert.Equal(discount.Code, order.DiscountCode);
    }

    [Fact]
    public async Task RemovingTheAppliedCode_DropsItFromTheCartTotal()
    {
        var admin = await LoginAdminAsync();
        var discount = await CreateOkAsync(admin, UniqueCode("RMV"), "fixed", 10_000);
        var variant = await CreateVariantAsync(admin, 200_000, 10);
        var shopper = NewShopper();
        await shopper.AddAsync(variant, 1);
        await shopper.SendAsync(HttpMethod.Post, "/api/v1/store/cart/discount", new { code = discount.Code });
        Assert.NotNull((await shopper.CartAsync()).Discount);

        Assert.Equal(HttpStatusCode.OK, (await shopper.SendAsync(HttpMethod.Delete, "/api/v1/store/cart/discount")).StatusCode);
        Assert.Null((await shopper.CartAsync()).Discount);
    }
}
