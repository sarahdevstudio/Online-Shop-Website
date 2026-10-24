using System.Net;

namespace Shop.IntegrationTests;

public sealed class AdminOrderTests(ApiFixture fixture) : StoreTestBase(fixture)
{
    private async Task<(PlacedBody Placed, int Id, int Variant)> NewOrderAsync(HttpClient admin, string method, int stock = 5, int quantity = 2)
    {
        var variant = await CreateVariantAsync(admin, 100_000, stock);
        var shopper = NewShopper();
        await shopper.AddAsync(variant, quantity);
        var placed = await PlaceOkAsync(shopper, method: method);
        return (placed, await OrderIdAsync(placed.Number), variant);
    }

    private static string Url(int id, string action) => $"/api/v1/admin/orders/{id}/{action}";

    [Fact]
    public async Task AdminEndpoints_RequireLogin_Permission_AndCsrfToken()
    {
        var admin = await LoginAdminAsync();
        var (_, id, _) = await NewOrderAsync(admin, "cashOnDelivery");

        Assert.Equal(HttpStatusCode.Unauthorized, (await NewClient().GetAsync("/api/v1/admin/orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await NewClient().GetAsync($"/api/v1/admin/orders/{id}")).StatusCode);

        var (customer, _, _) = await RegisterCustomerAsync(); // logged in, but no Orders.Manage
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/v1/admin/orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(customer, Url(id, "cancel"))).StatusCode);

        // The storefront key is not an admin credential.
        var shopper = NewShopper();
        Assert.Equal(HttpStatusCode.Unauthorized, (await shopper.SendAsync(HttpMethod.Get, "/api/v1/admin/orders")).StatusCode);

        // Logged in as admin but without the antiforgery header: rejected, and nothing changed.
        var noCsrf = await admin.PostAsync(Url(id, "cancel"), content: null);
        await AssertProblemAsync(noCsrf, 400, "antiforgery_invalid");
        Assert.Equal("pending", (await AdminOrderAsync(admin, id)).Order.Status);
    }

    [Fact]
    public async Task List_FiltersByStatus_PaymentStatus_AndSearch()
    {
        var admin = await LoginAdminAsync();
        var (placed, id, _) = await NewOrderAsync(admin, "cardToCard");

        var byNumber = await GetOkAsync<AdminOrderListBody>(admin, $"/api/v1/admin/orders?q={placed.Number}");
        Assert.Equal(id, Assert.Single(byNumber.Items).Id);
        Assert.True(byNumber.Items[0].Total > 0);

        var awaiting = await GetOkAsync<AdminOrderListBody>(admin, "/api/v1/admin/orders?status=awaitingPayment&paymentStatus=unpaid");
        Assert.Contains(awaiting.Items, o => o.Id == id);
        var paid = await GetOkAsync<AdminOrderListBody>(admin, "/api/v1/admin/orders?paymentStatus=paid&q=" + placed.Number);
        Assert.Empty(paid.Items);
    }

    [Fact]
    public async Task CardToCard_ConfirmPayment_MarksPaidAndConfirmed_AndIsLogged()
    {
        var admin = await LoginAdminAsync();
        var (_, id, _) = await NewOrderAsync(admin, "cardToCard");

        var confirmed = await SendOkAsync<AdminOrderBody>(admin, HttpMethod.Post, Url(id, "confirm-payment"));

        Assert.Equal("confirmed", confirmed.Order.Status);
        Assert.Equal("paid", confirmed.Order.PaymentStatus);
        Assert.Null(confirmed.Order.PaymentInstructions); // no longer needed once paid
        Assert.Equal(new[] { "awaitingPayment", "confirmed" }, confirmed.StatusLog.Select(l => l.To));
        Assert.Null(confirmed.StatusLog[0].AdminUserId);
        Assert.NotNull(confirmed.StatusLog[1].AdminUserId);
        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Post, Url(id, "confirm-payment")), 409, "transition_not_allowed");
    }

    [Fact]
    public async Task CashOnDelivery_FollowsTheStatusFlow_AndIsPaidOnDelivery()
    {
        var admin = await LoginAdminAsync();
        var (_, id, _) = await NewOrderAsync(admin, "cashOnDelivery");

        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Post, Url(id, "confirm-payment")), 409, "transition_not_allowed");
        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Post, Url(id, "status"), new { status = "shipped" }), 409, "transition_not_allowed");
        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Post, Url(id, "status"), new { status = "cancelled" }), 409, "status_not_allowed");

        var confirmed = await SendOkAsync<AdminOrderBody>(admin, HttpMethod.Post, Url(id, "status"), new { status = "confirmed" });
        Assert.Equal("unpaid", confirmed.Order.PaymentStatus);
        await SendOkAsync<AdminOrderBody>(admin, HttpMethod.Post, Url(id, "status"), new { status = "shipped" });
        var delivered = await SendOkAsync<AdminOrderBody>(admin, HttpMethod.Post, Url(id, "status"), new { status = "delivered" });

        Assert.Equal("delivered", delivered.Order.Status);
        Assert.Equal("paid", delivered.Order.PaymentStatus);
        Assert.Equal(4, delivered.StatusLog.Length);
        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Post, Url(id, "cancel")), 409, "transition_not_allowed");
    }

    [Fact]
    public async Task Cancel_RestoresStock_ExactlyOnce_AndNotAfterShipping()
    {
        var admin = await LoginAdminAsync();
        var (_, id, variant) = await NewOrderAsync(admin, "cashOnDelivery", stock: 5, quantity: 2);
        Assert.Equal(3, await StockOfAsync(variant));

        var cancelled = await SendOkAsync<AdminOrderBody>(admin, HttpMethod.Post, Url(id, "cancel"));
        Assert.Equal("cancelled", cancelled.Order.Status);
        Assert.Equal(5, await StockOfAsync(variant));

        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Post, Url(id, "cancel")), 409, "transition_not_allowed");
        Assert.Equal(5, await StockOfAsync(variant)); // not restored twice

        var (_, shippedId, shippedVariant) = await NewOrderAsync(admin, "cashOnDelivery", stock: 5, quantity: 1);
        await SendOkAsync<AdminOrderBody>(admin, HttpMethod.Post, Url(shippedId, "status"), new { status = "confirmed" });
        await SendOkAsync<AdminOrderBody>(admin, HttpMethod.Post, Url(shippedId, "status"), new { status = "shipped" });
        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Post, Url(shippedId, "cancel")), 409, "transition_not_allowed");
        Assert.Equal(4, await StockOfAsync(shippedVariant));
    }

    [Fact]
    public async Task CancelAfterPaymentConfirmed_IsAllowed_AndRestoresStock()
    {
        var admin = await LoginAdminAsync();
        var (_, id, variant) = await NewOrderAsync(admin, "cardToCard", stock: 4, quantity: 3);
        await SendOkAsync<AdminOrderBody>(admin, HttpMethod.Post, Url(id, "confirm-payment"));

        var cancelled = await SendOkAsync<AdminOrderBody>(admin, HttpMethod.Post, Url(id, "cancel"));

        Assert.Equal("cancelled", cancelled.Order.Status);
        Assert.Equal("paid", cancelled.Order.PaymentStatus); // refunds are manual in the MVP
        Assert.Equal(4, await StockOfAsync(variant));
    }

    [Fact]
    public async Task SimultaneousCancels_RestoreTheStockOnlyOnce()
    {
        var admin = await LoginAdminAsync();
        var admin2 = await LoginAdminAsync();
        var (_, id, variant) = await NewOrderAsync(admin, "cashOnDelivery", stock: 5, quantity: 2);

        var results = await Task.WhenAll(
            SendAsync(admin, HttpMethod.Post, Url(id, "cancel")),
            SendAsync(admin2, HttpMethod.Post, Url(id, "cancel")));

        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(5, await StockOfAsync(variant));
    }

    [Fact]
    public async Task UnknownOrder_Is404()
    {
        var admin = await LoginAdminAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/v1/admin/orders/2000000000")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(admin, HttpMethod.Post, Url(2_000_000_000, "cancel"))).StatusCode);
    }
}
