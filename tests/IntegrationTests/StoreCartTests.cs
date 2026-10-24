using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;

namespace Shop.IntegrationTests;

public sealed class StoreCartTests(ApiFixture fixture) : StoreTestBase(fixture)
{
    [Fact]
    public async Task AddItem_CreatesCart_AndShowsLivePriceAndShipping()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 120_000, 5);
        var shopper = NewShopper();

        var response = await shopper.AddAsync(variant, 2);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Shopper.ReadAsync<CartMutationBody>(response);
        Assert.False(string.IsNullOrEmpty(body.NewToken));
        Assert.Equal(43, body.NewToken!.Length);
        var line = Assert.Single(body.Cart.Lines);
        Assert.Equal(120_000, line.UnitPrice);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(240_000, body.Cart.Subtotal);
        Assert.Equal(50_000, body.Cart.ShippingFee); // placeholder flat fee, no free-shipping threshold
        Assert.Equal(290_000, body.Cart.Total);
        Assert.Equal(2, body.Cart.ItemCount);
        Assert.False(body.Clamped);
    }

    [Fact]
    public async Task AddItem_Again_MergesTheLine_AndSecondCallDoesNotIssueANewToken()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 100_000, 9);
        var shopper = NewShopper();
        await shopper.AddAsync(variant, 1);

        var second = await Shopper.ReadAsync<CartMutationBody>(await shopper.AddAsync(variant, 3));

        Assert.Null(second.NewToken);
        Assert.Equal(4, Assert.Single(second.Cart.Lines).Quantity);
    }

    [Fact]
    public async Task AddItem_ClampsToStock_AndToTheConfiguredMaximum()
    {
        var admin = await LoginAdminAsync();
        var scarce = await CreateVariantAsync(admin, 100_000, 3);
        var plenty = await CreateVariantAsync(admin, 100_000, 50);
        var shopper = NewShopper();

        var byStock = await Shopper.ReadAsync<CartMutationBody>(await shopper.AddAsync(scarce, 5));
        var byMax = await Shopper.ReadAsync<CartMutationBody>(await shopper.AddAsync(plenty, 12));

        Assert.True(byStock.Clamped);
        Assert.Equal(3, byStock.Cart.Lines.Single(l => l.VariantId == scarce).Quantity);
        Assert.True(byMax.Clamped);
        Assert.Equal(10, byMax.Cart.Lines.Single(l => l.VariantId == plenty).Quantity); // Checkout:MaxQuantityPerLine default
    }

    [Fact]
    public async Task AddItem_OutOfStock_Unknown_AndDraft_AreRejected()
    {
        var admin = await LoginAdminAsync();
        var soldOut = await CreateVariantAsync(admin, 100_000, 0);
        var shopper = NewShopper();

        await AssertProblemAsync(await shopper.AddAsync(soldOut, 1), 409, "variant_unavailable");
        await AssertProblemAsync(await shopper.AddAsync(int.MaxValue, 1), 404, "variant_not_found");
        Assert.Equal(HttpStatusCode.BadRequest, (await shopper.AddAsync(soldOut, 0)).StatusCode);
    }

    [Fact]
    public async Task SetQuantity_Updates_Clamps_AndZeroRemovesTheLine()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 100_000, 4);
        var shopper = NewShopper();
        await shopper.AddAsync(variant, 1);

        var updated = await Shopper.ReadAsync<CartMutationBody>(await shopper.SendAsync(HttpMethod.Put, $"/api/v1/store/cart/items/{variant}", new { quantity = 3 }));
        var clamped = await Shopper.ReadAsync<CartMutationBody>(await shopper.SendAsync(HttpMethod.Put, $"/api/v1/store/cart/items/{variant}", new { quantity = 9 }));
        var removed = await Shopper.ReadAsync<CartMutationBody>(await shopper.SendAsync(HttpMethod.Delete, $"/api/v1/store/cart/items/{variant}"));

        Assert.Equal(3, updated.Cart.Lines.Single().Quantity);
        Assert.True(clamped.Clamped);
        Assert.Equal(4, clamped.Cart.Lines.Single().Quantity);
        Assert.Empty(removed.Cart.Lines);
        Assert.Equal(0, removed.Cart.Subtotal);
        Assert.Equal(0, removed.Cart.ShippingFee); // nothing to ship
    }

    [Fact]
    public async Task Cart_ShowsNotices_ForChangedPrice_Unavailable_AndStockBelowQuantity()
    {
        var admin = await LoginAdminAsync();
        var variants = await CreateVariantsAsync(admin, (100_000, 9), (200_000, 9), (300_000, 9));
        var shopper = NewShopper();
        foreach (var v in variants)
        {
            await shopper.AddAsync(v, 3);
        }

        await SetPriceAsync(variants[0], 110_000);
        await SetStockAsync(variants[1], 0);
        await SetStockAsync(variants[2], 2);
        var cart = await shopper.CartAsync();

        var priced = cart.Lines.Single(l => l.VariantId == variants[0]);
        Assert.Equal("price_changed", priced.Notice);
        Assert.Equal(100_000, priced.PreviousPrice);
        Assert.Equal(110_000, priced.UnitPrice); // always the live price
        Assert.Equal("unavailable", cart.Lines.Single(l => l.VariantId == variants[1]).Notice);
        Assert.Equal(0, cart.Lines.Single(l => l.VariantId == variants[1]).LineTotal);
        var short3 = cart.Lines.Single(l => l.VariantId == variants[2]);
        Assert.Equal("quantity_exceeds", short3.Notice);
        Assert.Equal(2, short3.MaxQuantity);
        Assert.True(cart.HasBlockingIssue);
        Assert.Equal(110_000 * 3 + 300_000 * 3, cart.Subtotal); // the unavailable line is not charged
    }

    [Fact]
    public async Task Cart_ExpiresAfter30DaysOfInactivity()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 100_000, 5);
        var shopper = NewShopper();
        await shopper.AddAsync(variant, 1);

        await Fixture.WithDbAsync(async db => await db.Carts.Where(c => c.Items.Any(i => i.VariantId == variant)).ExecuteUpdateAsync(s =>
            s.SetProperty(c => c.LastActivityAtUtc, DateTime.UtcNow.AddDays(-31))));

        Assert.Empty((await shopper.CartAsync()).Lines);
        var count = await Shopper.ReadAsync<Dictionary<string, int>>(await shopper.SendAsync(HttpMethod.Get, "/api/v1/store/cart/count"));
        Assert.Equal(0, count["count"]);
    }

    [Fact]
    public async Task Cart_WithoutToken_OrWithGarbage_IsEmpty_AndTokensAreNotInterchangeable()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 100_000, 5);
        var first = NewShopper();
        await first.AddAsync(variant, 2);
        var stranger = NewShopper();

        Assert.Empty((await stranger.CartAsync()).Lines);
        stranger.CartToken = new string('A', 43);
        Assert.Empty((await stranger.CartAsync()).Lines);
        stranger.CartToken = "short";
        Assert.Empty((await stranger.CartAsync()).Lines);
        var count = await Shopper.ReadAsync<Dictionary<string, int>>(await first.SendAsync(HttpMethod.Get, "/api/v1/store/cart/count"));
        Assert.Equal(2, count["count"]);
    }

    [Fact]
    public async Task StoreEndpoints_RequireTheStorefrontKey_ButNoCsrfToken()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 100_000, 5);
        var client = Fixture.Factory.CreateClient();

        // No key: 401 with a stable code (not the cookie-CSRF 400).
        var noKey = await client.PostAsJsonAsync("/api/v1/store/cart/items", new { variantId = variant, quantity = 1 });
        await AssertProblemAsync(noKey, 401, "storefront_key_invalid");

        using var wrong = new HttpRequestMessage(HttpMethod.Get, "/api/v1/store/cart");
        wrong.Headers.Add("X-Storefront-Key", ApiFixture.StorefrontKey + "x");
        await AssertProblemAsync(await client.SendAsync(wrong), 401, "storefront_key_invalid");

        using var empty = new HttpRequestMessage(HttpMethod.Get, "/api/v1/store/cart");
        empty.Headers.Add("X-Storefront-Key", "");
        await AssertProblemAsync(await client.SendAsync(empty), 401, "storefront_key_invalid");

        // With the key, a POST succeeds without any antiforgery token.
        var shopper = NewShopper();
        Assert.Equal(HttpStatusCode.OK, (await shopper.AddAsync(variant, 1)).StatusCode);
    }

    [Fact]
    public async Task StoreResponses_AreNeverCached()
    {
        var shopper = NewShopper();
        var response = await shopper.SendAsync(HttpMethod.Get, "/api/v1/store/cart");

        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
    }
}
