using Microsoft.EntityFrameworkCore;

namespace Shop.IntegrationTests;

public sealed record ReportsSummaryBody(
    long Revenue, long ShippingCollected, long CostOfGoods, long GrossProfit, decimal MarginPercent,
    int OrderCount, long AverageOrderValue, int OrdersWithoutCost, int ItemsWithoutCost);

public sealed record ReportsDailyBody(DateTime Date, long Revenue, long Profit, int OrderCount);

public sealed record ReportsProductBody(int ProductId, string ProductName, string Slug, long Revenue, long Profit);

public sealed record ReportsBody(
    DateTime FromUtc, DateTime ToUtc, ReportsSummaryBody Summary, ReportsDailyBody[] Daily,
    ReportsProductBody[] TopByRevenue, ReportsProductBody[] TopByProfit);

/// <summary>
/// F2 (docs/19): the worked example in the task's own report comes straight from this test. Counted statuses
/// (confirmed/shipped/delivered) only; a cost-less line is skipped from cost/profit, never treated as free.
/// </summary>
public sealed class ReportsTests(ApiFixture fixture) : StoreTestBase(fixture)
{
    private const string Url = "/api/v1/admin/reports";

    private async Task<int> VariantWithCostAsync(HttpClient admin, long price, long? cost)
    {
        var id = await CreateVariantAsync(admin, price, 100);
        if (cost is not null)
        {
            await Fixture.WithDbAsync(async db => await db.ProductVariants.Where(v => v.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.CostPrice, cost)));
        }

        return id;
    }

    private Task SetCreatedAsync(int orderId, DateTime createdUtc) =>
        Fixture.WithDbAsync(async db => await db.Orders.Where(o => o.Id == orderId)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.CreatedAtUtc, createdUtc)));

    [Fact]
    public async Task Summary_DailySeries_AndTop10_MatchAHandComputedWorkedExample()
    {
        var admin = await LoginAdminAsync();

        // Variant A: cost known (50,000). Variant B: no cost price yet.
        var variantA = await VariantWithCostAsync(admin, 100_000, 50_000);
        var variantB = await VariantWithCostAsync(admin, 80_000, null);

        // Day 1 (Tehran), 08:00 local = 04:30 UTC. Order 1: 2x A, confirmed. Revenue 200,000, cost 100,000.
        var day1Utc = new DateTime(2026, 6, 1, 4, 30, 0, DateTimeKind.Utc);
        var shopper1 = NewShopper();
        await shopper1.AddAsync(variantA, 2);
        var placed1 = await PlaceOkAsync(shopper1);
        var order1 = await OrderIdAsync(placed1.Number);
        await SendOkAsync<AdminOrderBody>(admin, HttpMethod.Post, $"/api/v1/admin/orders/{order1}/status", new { status = "confirmed" });
        await SetCreatedAsync(order1, day1Utc);

        // Same day: order 2, 1x B (no cost), confirmed. Revenue 80,000, no cost counted.
        var shopper2 = NewShopper();
        await shopper2.AddAsync(variantB, 1);
        var placed2 = await PlaceOkAsync(shopper2);
        var order2 = await OrderIdAsync(placed2.Number);
        await SendOkAsync<AdminOrderBody>(admin, HttpMethod.Post, $"/api/v1/admin/orders/{order2}/status", new { status = "confirmed" });
        await SetCreatedAsync(order2, day1Utc.AddHours(2));

        // Same day: order 3, 1x A, left pending — never counted.
        var shopper3 = NewShopper();
        await shopper3.AddAsync(variantA, 1);
        var placed3 = await PlaceOkAsync(shopper3);
        await SetCreatedAsync(await OrderIdAsync(placed3.Number), day1Utc);

        // Same day: order 4, 1x A, confirmed then cancelled — never counted.
        var shopper4 = NewShopper();
        await shopper4.AddAsync(variantA, 1);
        var placed4 = await PlaceOkAsync(shopper4);
        var order4 = await OrderIdAsync(placed4.Number);
        await SendOkAsync<AdminOrderBody>(admin, HttpMethod.Post, $"/api/v1/admin/orders/{order4}/status", new { status = "confirmed" });
        var cancelledOrder4 = await SendOkAsync<AdminOrderBody>(admin, HttpMethod.Post, $"/api/v1/admin/orders/{order4}/cancel");
        Assert.Equal("cancelled", cancelledOrder4.Order.Status);
        await SetCreatedAsync(order4, day1Utc);

        // The Tehran day 2026-06-01 is [2026-05-31 20:30 UTC, 2026-06-01 20:30 UTC).
        var fromUtc = new DateTime(2026, 5, 31, 20, 30, 0, DateTimeKind.Utc);
        var toUtc = new DateTime(2026, 6, 1, 20, 30, 0, DateTimeKind.Utc);
        var report = await GetOkAsync<ReportsBody>(admin, $"{Url}?from={fromUtc:O}&to={toUtc:O}");

        Assert.Equal(2, report.Summary.OrderCount); // only the two confirmed orders
        Assert.Equal(280_000, report.Summary.Revenue); // 200,000 + 80,000
        Assert.Equal(100_000, report.Summary.ShippingCollected); // 2 orders × the default flat fee (50,000)
        Assert.Equal(100_000, report.Summary.CostOfGoods); // only order 1's known-cost line
        Assert.Equal(180_000, report.Summary.GrossProfit); // 280,000 − 100,000
        Assert.Equal(64.3m, report.Summary.MarginPercent); // 180,000 / 280,000
        Assert.Equal(190_000, report.Summary.AverageOrderValue); // (250,000 + 130,000) / 2
        Assert.Equal(1, report.Summary.OrdersWithoutCost);
        Assert.Equal(1, report.Summary.ItemsWithoutCost);

        var day = Assert.Single(report.Daily);
        Assert.Equal(new DateTime(2026, 6, 1), day.Date);
        Assert.Equal(280_000, day.Revenue);
        Assert.Equal(100_000, day.Profit); // order 2's line has no known cost, so it adds 0 to profit, not its full revenue
        Assert.Equal(2, day.OrderCount);

        Assert.Equal(2, report.TopByRevenue.Length);
        Assert.Equal(200_000, report.TopByRevenue[0].Revenue); // product A first
        Assert.Equal(80_000, report.TopByRevenue[1].Revenue); // product B second

        Assert.Equal(100_000, report.TopByProfit[0].Profit); // product A
        Assert.Equal(0, report.TopByProfit[1].Profit); // product B: no cost-known line, 0 - never treated as negative or guessed
    }

    [Fact]
    public async Task ADayBoundaryOrder_IsBucketedByTehranMidnight_NotUtcMidnight()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 100_000, 10);

        // 20:29 UTC is still the previous Tehran day (Tehran midnight is 20:30 UTC); 20:31 UTC is already the next one.
        // A different month than the other test in this file, so a shared test database never mixes the two.
        var justBefore = new DateTime(2026, 7, 1, 20, 29, 0, DateTimeKind.Utc);
        var justAfter = new DateTime(2026, 7, 1, 20, 31, 0, DateTimeKind.Utc);

        async Task<int> PlaceConfirmedAsync(DateTime createdUtc)
        {
            var shopper = NewShopper();
            await shopper.AddAsync(variant, 1);
            var placed = await PlaceOkAsync(shopper);
            var id = await OrderIdAsync(placed.Number);
            await SendOkAsync<AdminOrderBody>(admin, HttpMethod.Post, $"/api/v1/admin/orders/{id}/status", new { status = "confirmed" });
            await SetCreatedAsync(id, createdUtc);
            return id;
        }

        await PlaceConfirmedAsync(justBefore);
        await PlaceConfirmedAsync(justAfter);

        var report = await GetOkAsync<ReportsBody>(admin, $"{Url}?from={justBefore.AddDays(-1):O}&to={justAfter.AddDays(1):O}");
        Assert.Equal(2, report.Daily.Length);
        Assert.Equal(new DateTime(2026, 7, 1), report.Daily[0].Date);
        Assert.Equal(new DateTime(2026, 7, 2), report.Daily[1].Date);
        Assert.All(report.Daily, d => Assert.Equal(1, d.OrderCount));
    }
}
