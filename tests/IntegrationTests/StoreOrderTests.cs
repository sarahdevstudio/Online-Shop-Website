using System.Net;
using Microsoft.EntityFrameworkCore;

namespace Shop.IntegrationTests;

public sealed class StoreOrderTests(ApiFixture fixture) : StoreTestBase(fixture)
{
    [Fact]
    public async Task CashOnDelivery_PlacesOrder_SnapshotsLines_DecrementsStock_AndClearsTheCart()
    {
        var admin = await LoginAdminAsync();
        var variants = await CreateVariantsAsync(admin, (100_000, 5), (250_000, 3));
        var shopper = NewShopper();
        await shopper.AddAsync(variants[0], 2);
        await shopper.AddAsync(variants[1], 1);

        var mobile = NewMobile();
        var persianMobile = string.Concat(mobile.Select(c => (char)('۰' + (c - '0'))));
        var (response, _) = await PlaceAsync(shopper, mobile: persianMobile); // Persian digits are accepted
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var placed = await Shopper.ReadAsync<PlacedBody>(response);

        Assert.False(placed.Replayed);
        Assert.True(placed.Number >= 1001);
        var order = await GetOrderAsync(shopper, placed.AccessToken);
        Assert.Equal("pending", order.Status);
        Assert.Equal("unpaid", order.PaymentStatus);
        Assert.Equal("cashOnDelivery", order.PaymentMethod);
        Assert.Equal(mobile, order.Mobile);
        Assert.Equal(450_000, order.Subtotal);
        Assert.Equal(50_000, order.ShippingFee);
        Assert.Equal(500_000, order.Total);
        Assert.Equal(2, order.Lines.Length);
        Assert.All(order.Lines, l => Assert.False(string.IsNullOrEmpty(l.Sku)));
        Assert.Null(order.PaymentInstructions);

        Assert.Equal(3, await StockOfAsync(variants[0]));
        Assert.Equal(2, await StockOfAsync(variants[1]));
        Assert.Empty((await shopper.CartAsync()).Lines);
    }

    [Fact]
    public async Task CardToCard_ShowsExactAmountAndCard_AcceptsTransferReference_UntilConfirmed()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 100_000, 5);
        var shopper = NewShopper();
        await shopper.AddAsync(variant, 1);
        var placed = await PlaceOkAsync(shopper, method: "cardToCard");

        var order = await GetOrderAsync(shopper, placed.AccessToken);
        Assert.Equal("awaitingPayment", order.Status);
        Assert.Equal(150_000, order.PaymentInstructions!.Amount);
        Assert.False(string.IsNullOrEmpty(order.PaymentInstructions.CardNumber));
        Assert.False(string.IsNullOrEmpty(order.PaymentInstructions.HolderName));

        var url = $"/api/v1/store/orders/{placed.AccessToken}/transfer-reference";
        var first = await shopper.SendAsync(HttpMethod.Post, url, new { tracking = "۱۲۳۴۵۶۷۸", time = "۱۴۰۵/۰۸/۰۲ ۱۰:۳۰", cardLast4 = "۶۰۳۷" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var again = await shopper.SendAsync(HttpMethod.Post, url, new { tracking = "99887766", time = "امروز ۱۱:۰۰", cardLast4 = "1111" });
        var updated = await Shopper.ReadAsync<OrderBody>(again);
        Assert.Equal("99887766", updated.TransferReference!.Tracking);

        Assert.Equal(HttpStatusCode.BadRequest, (await shopper.SendAsync(HttpMethod.Post, url, new { tracking = "1", time = "", cardLast4 = "12" })).StatusCode);

        var id = await OrderIdAsync(placed.Number);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(admin, HttpMethod.Post, $"/api/v1/admin/orders/{id}/confirm-payment")).StatusCode);
        await AssertProblemAsync(
            await shopper.SendAsync(HttpMethod.Post, url, new { tracking = "55555555", time = "x", cardLast4 = "1234" }), 409, "not_awaiting_payment");
    }

    [Fact]
    public async Task TransferReference_IsRejected_ForCashOnDeliveryOrders()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 100_000, 5);
        var shopper = NewShopper();
        await shopper.AddAsync(variant, 1);
        var placed = await PlaceOkAsync(shopper);

        var response = await shopper.SendAsync(HttpMethod.Post, $"/api/v1/store/orders/{placed.AccessToken}/transfer-reference",
            new { tracking = "12345678", time = "now", cardLast4 = "1234" });

        await AssertProblemAsync(response, 409, "not_awaiting_payment");
    }

    [Fact]
    public async Task ExpectedTotalMismatch_Returns409_LeavesStockAndCartUntouched()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 100_000, 5);
        var shopper = NewShopper();
        await shopper.AddAsync(variant, 1);
        var seen = (await shopper.CartAsync()).Total;

        await SetPriceAsync(variant, 120_000); // changes while the customer is filling in the form
        var (response, _) = await PlaceAsync(shopper, expectedTotal: seen);

        var problem = await Shopper.ReadAsync<StockProblemBody>(response);
        Assert.Equal(409, problem.Status);
        Assert.Equal("price_changed", problem.Code);
        Assert.Equal(170_000, problem.Total);
        Assert.Equal(5, await StockOfAsync(variant));
        Assert.Single((await shopper.CartAsync()).Lines);

        // Re-reviewing the cart and submitting the new total works.
        var (retry, _) = await PlaceAsync(shopper);
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
    }

    [Fact]
    public async Task OrderSnapshot_IsNotAffectedByLaterCatalogChanges()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 100_000, 5);
        var shopper = NewShopper();
        await shopper.AddAsync(variant, 1);
        var placed = await PlaceOkAsync(shopper);

        await SetPriceAsync(variant, 999_000);
        var order = await GetOrderAsync(shopper, placed.AccessToken);

        Assert.Equal(100_000, order.Lines.Single().UnitPrice);
        Assert.Equal(150_000, order.Total);
    }

    [Fact]
    public async Task InsufficientStock_OnAnyLine_RollsBackEverything_AndNamesTheLines()
    {
        var admin = await LoginAdminAsync();
        var variants = await CreateVariantsAsync(admin, (100_000, 5), (100_000, 5));
        var shopper = NewShopper();
        await shopper.AddAsync(variants[0], 2);
        await shopper.AddAsync(variants[1], 4);
        var total = (await shopper.CartAsync()).Total;
        await SetStockAsync(variants[1], 1); // sold elsewhere meanwhile

        var (response, _) = await PlaceAsync(shopper, expectedTotal: total);

        var problem = await Shopper.ReadAsync<StockProblemBody>(response);
        Assert.Equal(409, problem.Status);
        Assert.Equal("stock_insufficient", problem.Code);
        var line = Assert.Single(problem.Lines!);
        Assert.Equal(variants[1], line.VariantId);
        Assert.Equal(4, line.Requested);
        Assert.Equal(1, line.Available);
        Assert.Equal(5, await StockOfAsync(variants[0])); // the first line was not decremented
        Assert.Equal(1, await StockOfAsync(variants[1]));
        Assert.Equal(2, (await shopper.CartAsync()).Lines.Length); // cart kept so the customer can adjust
    }

    [Fact]
    public async Task EmptyCart_Cannot_PlaceAnOrder()
    {
        var shopper = NewShopper();

        var (response, _) = await PlaceAsync(shopper, expectedTotal: 0);

        await AssertProblemAsync(response, 409, "cart_empty");
    }

    [Fact]
    public async Task SameIdempotencyKey_ReturnsTheSameOrder_AndDecrementsOnce()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 100_000, 5);
        var shopper = NewShopper();
        await shopper.AddAsync(variant, 2);
        var mobile = NewMobile();
        var key = Guid.NewGuid();

        var (firstResponse, _) = await PlaceAsync(shopper, mobile, key: key);
        var first = await Shopper.ReadAsync<PlacedBody>(firstResponse);
        // Double click / browser back: the cart is already gone, the key is not.
        var (secondResponse, _) = await PlaceAsync(shopper, mobile, expectedTotal: 250_000, key: key);
        var second = await Shopper.ReadAsync<PlacedBody>(secondResponse);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(first.Number, second.Number);
        Assert.Equal(first.AccessToken, second.AccessToken);
        Assert.True(second.Replayed);
        Assert.Equal(3, await StockOfAsync(variant));
        Assert.Equal(1, await Fixture.WithDbAsync(db => db.Orders.CountAsync(o => o.Mobile == mobile)));
    }

    [Fact]
    public async Task SameIdempotencyKey_SentInParallel_CreatesOneOrder_EvenForTheLastItem()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 100_000, 1);
        var shopper = NewShopper();
        await shopper.AddAsync(variant, 1);
        var total = (await shopper.CartAsync()).Total;
        var mobile = NewMobile();
        var key = Guid.NewGuid();

        var responses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => shopper.SendAsync(HttpMethod.Post, "/api/v1/store/orders", Checkout(mobile, "cashOnDelivery", total), key)));

        Assert.All(responses, r => Assert.True(r.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK, r.StatusCode.ToString()));
        var bodies = await Task.WhenAll(responses.Select(Shopper.ReadAsync<PlacedBody>));
        Assert.Single(bodies.Select(b => b.Number).Distinct());
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(0, await StockOfAsync(variant));
        Assert.Equal(1, await Fixture.WithDbAsync(db => db.Orders.CountAsync(o => o.IdempotencyKey == key)));
    }

    [Fact]
    public async Task TwoCheckoutsForTheLastItem_ExactlyOneWins_StockNeverNegative()
    {
        var admin = await LoginAdminAsync();
        for (var round = 0; round < 6; round++)
        {
            var variant = await CreateVariantAsync(admin, 100_000, 1);
            var a = NewShopper();
            var b = NewShopper();
            await a.AddAsync(variant, 1);
            await b.AddAsync(variant, 1);
            var totalA = (await a.CartAsync()).Total;
            var totalB = (await b.CartAsync()).Total;
            var start = new TaskCompletionSource();

            async Task<HttpResponseMessage> Go(Shopper s, long total)
            {
                await start.Task;
                return await s.SendAsync(HttpMethod.Post, "/api/v1/store/orders", Checkout(NewMobile(), "cashOnDelivery", total), Guid.NewGuid());
            }

            var tasks = new[] { Go(a, totalA), Go(b, totalB) };
            start.SetResult();
            var responses = await Task.WhenAll(tasks);

            Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
            var loser = Assert.Single(responses, r => r.StatusCode != HttpStatusCode.Created);
            var problem = await Shopper.ReadAsync<StockProblemBody>(loser);
            Assert.Equal("stock_insufficient", problem.Code);
            Assert.Equal(variant, Assert.Single(problem.Lines!).VariantId);
            Assert.Equal(0, await StockOfAsync(variant));
        }
    }

    [Fact]
    public async Task ManyBuyersForLimitedStock_SellExactlyTheStock_WithoutDeadlocks()
    {
        var admin = await LoginAdminAsync();
        var variants = await CreateVariantsAsync(admin, (100_000, 4), (100_000, 4));
        var shoppers = Enumerable.Range(0, 8).Select(_ => NewShopper()).ToList();
        foreach (var (shopper, i) in shoppers.Select((s, i) => (s, i)))
        {
            // Opposite insertion orders; checkout still locks stock rows in variant-id order.
            var (first, second) = i % 2 == 0 ? (variants[0], variants[1]) : (variants[1], variants[0]);
            await shopper.AddAsync(first, 1);
            await shopper.AddAsync(second, 1);
        }

        var totals = await Task.WhenAll(shoppers.Select(s => s.CartAsync()));
        var responses = await Task.WhenAll(shoppers.Select((s, i) =>
            s.SendAsync(HttpMethod.Post, "/api/v1/store/orders", Checkout(NewMobile(), "cashOnDelivery", totals[i].Total), Guid.NewGuid())));

        Assert.Equal(4, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(4, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(0, await StockOfAsync(variants[0]));
        Assert.Equal(0, await StockOfAsync(variants[1]));
    }

    [Fact]
    public async Task Validation_RejectsBadFields_WithPersianMessages()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 100_000, 5);
        var shopper = NewShopper();
        await shopper.AddAsync(variant, 1);
        var total = (await shopper.CartAsync()).Total;

        var bad = await shopper.SendAsync(HttpMethod.Post, "/api/v1/store/orders", new
        {
            fullName = "a",
            mobile = "12345",
            province = "",
            city = "ت",
            address = "کوتاه",
            postalCode = "123",
            paymentMethod = "cashOnDelivery",
            expectedTotal = total,
        }, Guid.NewGuid());

        var problem = await AssertProblemAsync(bad, 400);
        Assert.NotNull(problem.Errors);
        foreach (var field in new[] { "FullName", "Mobile", "Province", "City", "Address", "PostalCode" })
        {
            Assert.True(problem.Errors!.ContainsKey(field), field);
        }

        // No idempotency key at all.
        var noKey = await shopper.SendAsync(HttpMethod.Post, "/api/v1/store/orders", Checkout(NewMobile(), "cashOnDelivery", total));
        Assert.Equal(HttpStatusCode.BadRequest, noKey.StatusCode);
        Assert.Equal(5, await StockOfAsync(variant));
    }

    [Theory]
    [InlineData("09123456789", "09123456789")]
    [InlineData("۰۹۱۲۳۴۵۶۷۸۹", "09123456789")]
    [InlineData("+98 912 345 6789", "09123456789")]
    [InlineData("00989123456789", "09123456789")]
    [InlineData("9123456789", "09123456789")]
    public async Task Mobile_IsNormalized_OnTheOrder(string input, string expected)
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 100_000, 5);
        var shopper = NewShopper();
        await shopper.AddAsync(variant, 1);

        // A fresh mobile per case would defeat the normalization check, so lift the unpaid cap by cancelling afterwards.
        var placed = await PlaceOkAsync(shopper, mobile: input);

        Assert.Equal(expected, (await GetOrderAsync(shopper, placed.AccessToken)).Mobile);
        await SendAsync(admin, HttpMethod.Post, $"/api/v1/admin/orders/{await OrderIdAsync(placed.Number)}/cancel");
    }

    [Fact]
    public async Task OrderLookup_UnknownOrMalformedToken_Is404()
    {
        var shopper = NewShopper();

        Assert.Equal(HttpStatusCode.NotFound, (await shopper.SendAsync(HttpMethod.Get, "/api/v1/store/orders/" + new string('A', 43))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await shopper.SendAsync(HttpMethod.Get, "/api/v1/store/orders/short")).StatusCode);
    }

    [Fact]
    public async Task OrderNumbers_AreSequential()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 100_000, 10);
        var shopper = NewShopper();
        await shopper.AddAsync(variant, 1);
        var first = await PlaceOkAsync(shopper);
        await shopper.AddAsync(variant, 1);
        var second = await PlaceOkAsync(shopper);

        Assert.Equal(first.Number + 1, second.Number);
    }

    [Fact]
    public async Task OrderTokens_AreUnguessable_AndDifferPerOrder()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 100_000, 10);
        var shopper = NewShopper();
        await shopper.AddAsync(variant, 1);
        var first = await PlaceOkAsync(shopper);
        await shopper.AddAsync(variant, 1);
        var second = await PlaceOkAsync(shopper);

        Assert.Equal(43, first.AccessToken.Length);
        Assert.NotEqual(first.AccessToken, second.AccessToken);
    }
}
