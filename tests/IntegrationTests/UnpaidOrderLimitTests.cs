using System.Net;

namespace Shop.IntegrationTests;

/// <summary>At most 2 unpaid (AwaitingPayment / Pending) orders per mobile number and per IP, so stock can't be hoarded.</summary>
public sealed class UnpaidOrderLimitTests(ApiFixture fixture) : StoreTestBase(fixture)
{
    private async Task<HttpResponseMessage> TryOrderAsync(HttpClient admin, Shopper shopper, int variant, string mobile, string method = "cashOnDelivery")
    {
        await shopper.AddAsync(variant, 1);
        var (response, _) = await PlaceAsync(shopper, mobile, method);
        return response;
    }

    [Fact]
    public async Task ThirdUnpaidOrder_ForTheSameMobile_IsRejected_WithAPersianMessage_AndStockIsUntouched()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 100_000, 10);
        var mobile = NewMobile();

        Assert.Equal(HttpStatusCode.Created, (await TryOrderAsync(admin, NewShopper(), variant, mobile)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await TryOrderAsync(admin, NewShopper(), variant, mobile, "cardToCard")).StatusCode);
        var third = await TryOrderAsync(admin, NewShopper(), variant, mobile);

        var problem = await AssertProblemAsync(third, 409, "unpaid_limit");
        Assert.Contains("سفارش پرداخت‌نشده", problem.Detail);
        Assert.Contains("۲", problem.Detail); // the limit, in Persian digits
        Assert.Equal(8, await StockOfAsync(variant));

        // A different mobile (and IP) is unaffected.
        Assert.Equal(HttpStatusCode.Created, (await TryOrderAsync(admin, NewShopper(), variant, NewMobile())).StatusCode);
    }

    [Fact]
    public async Task ThirdUnpaidOrder_FromTheSameIp_IsRejected_EvenWithDifferentMobiles()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 100_000, 10);
        var sameIp = NewShopper();

        Assert.Equal(HttpStatusCode.Created, (await TryOrderAsync(admin, sameIp, variant, NewMobile())).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await TryOrderAsync(admin, sameIp, variant, NewMobile())).StatusCode);
        var third = await TryOrderAsync(admin, sameIp, variant, NewMobile());

        await AssertProblemAsync(third, 409, "unpaid_limit");
        Assert.Equal(HttpStatusCode.Created, (await TryOrderAsync(admin, NewShopper(), variant, NewMobile())).StatusCode);
    }

    [Fact]
    public async Task CancellingOrConfirmingPayment_FreesASlot()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 100_000, 10);
        var mobile = NewMobile();
        var first = NewShopper();
        await first.AddAsync(variant, 1);
        var cod = await PlaceOkAsync(first, mobile);
        var card = await PlaceOkAsyncFor(variant, mobile, "cardToCard");
        await AssertProblemAsync(await TryOrderAsync(admin, NewShopper(), variant, mobile), 409, "unpaid_limit");

        // Admin confirms the card transfer: that order is no longer "unpaid".
        await SendAsync(admin, HttpMethod.Post, $"/api/v1/admin/orders/{await OrderIdAsync(card.Number)}/confirm-payment");
        Assert.Equal(HttpStatusCode.Created, (await TryOrderAsync(admin, NewShopper(), variant, mobile)).StatusCode);
        await AssertProblemAsync(await TryOrderAsync(admin, NewShopper(), variant, mobile), 409, "unpaid_limit");

        // Cancelling the other one frees a slot again.
        await SendAsync(admin, HttpMethod.Post, $"/api/v1/admin/orders/{await OrderIdAsync(cod.Number)}/cancel");
        Assert.Equal(HttpStatusCode.Created, (await TryOrderAsync(admin, NewShopper(), variant, mobile)).StatusCode);
    }

    [Fact]
    public async Task ParallelOrders_ForOneMobile_NeverExceedTheLimit()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 100_000, 20);
        var mobile = NewMobile();
        var shoppers = Enumerable.Range(0, 6).Select(_ => NewShopper()).ToList();
        foreach (var s in shoppers)
        {
            await s.AddAsync(variant, 1);
        }

        var totals = await Task.WhenAll(shoppers.Select(s => s.CartAsync()));
        var responses = await Task.WhenAll(shoppers.Select((s, i) =>
            s.SendAsync(HttpMethod.Post, "/api/v1/store/orders", Checkout(mobile, "cashOnDelivery", totals[i].Total), Guid.NewGuid())));

        Assert.Equal(2, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(4, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(18, await StockOfAsync(variant));
    }

    private async Task<PlacedBody> PlaceOkAsyncFor(int variant, string mobile, string method)
    {
        var shopper = NewShopper();
        await shopper.AddAsync(variant, 1);
        return await PlaceOkAsync(shopper, mobile, method);
    }
}
