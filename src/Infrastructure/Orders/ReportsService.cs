using Microsoft.EntityFrameworkCore;
using Shop.Application.Admin;
using Shop.Domain.Common;
using Shop.Domain.Orders;
using Shop.Infrastructure.Persistence;

namespace Shop.Infrastructure.Orders;

/// <summary>Sales/profit reports (docs/19 F2). Counted orders: Confirmed, Shipped, Delivered.</summary>
internal sealed class ReportsService(ShopDbContext db) : IReportsService
{
    private static readonly OrderStatus[] CountedStatuses = [OrderStatus.Confirmed, OrderStatus.Shipped, OrderStatus.Delivered];

    public async Task<ReportsDto> GetAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var orders = await db.Orders.AsNoTracking()
            .Where(o => CountedStatuses.Contains(o.Status) && o.CreatedAtUtc >= fromUtc && o.CreatedAtUtc < toUtc)
            .Select(o => new { o.Id, o.CreatedAtUtc, o.Subtotal, o.ShippingFee, o.Total })
            .ToListAsync(ct);
        var orderIds = orders.Select(o => o.Id).ToList();
        var dateById = orders.ToDictionary(o => o.Id, o => TehranTime.LocalDate(o.CreatedAtUtc));

        var items = orderIds.Count == 0
            ? []
            : await (
                from i in db.OrderItems.AsNoTracking()
                where orderIds.Contains(i.OrderId)
                join v in db.ProductVariants.AsNoTracking() on i.VariantId equals v.Id
                join p in db.Products.AsNoTracking() on v.ProductId equals p.Id
                select new { i.OrderId, i.Quantity, i.UnitPrice, i.UnitCost, ProductId = p.Id, p.Name, p.Slug })
                .ToListAsync(ct);

        var revenue = orders.Sum(o => o.Subtotal);
        var shippingCollected = orders.Sum(o => o.ShippingFee);
        var costOfGoods = items.Where(i => i.UnitCost is not null).Sum(i => i.UnitCost!.Value * i.Quantity);
        var grossProfit = revenue - costOfGoods;
        var marginPercent = revenue > 0 ? Math.Round(grossProfit * 100m / revenue, 1) : 0m;
        var orderCount = orders.Count;
        var averageOrderValue = orderCount > 0 ? orders.Sum(o => o.Total) / orderCount : 0;
        var itemsWithoutCost = items.Count(i => i.UnitCost is null);
        var ordersWithoutCost = items.Where(i => i.UnitCost is null).Select(i => i.OrderId).Distinct().Count();

        var summary = new ReportsSummaryDto(revenue, shippingCollected, costOfGoods, grossProfit, marginPercent, orderCount, averageOrderValue, ordersWithoutCost, itemsWithoutCost);

        var daily = orders.GroupBy(o => dateById[o.Id])
            .Select(g => new ReportsDailyPointDto(
                g.Key,
                g.Sum(o => o.Subtotal),
                items.Where(i => dateById.TryGetValue(i.OrderId, out var d) && d == g.Key && i.UnitCost is not null)
                    .Sum(i => i.UnitPrice * i.Quantity - i.UnitCost!.Value * i.Quantity),
                g.Count()))
            .OrderBy(d => d.Date)
            .ToList();

        var byProduct = items.GroupBy(i => new { i.ProductId, i.Name, i.Slug })
            .Select(g => new ReportsProductDto(
                g.Key.ProductId, g.Key.Name, g.Key.Slug,
                g.Sum(i => i.UnitPrice * i.Quantity),
                g.Where(i => i.UnitCost is not null).Sum(i => i.UnitPrice * i.Quantity - i.UnitCost!.Value * i.Quantity)))
            .ToList();

        var topByRevenue = byProduct.OrderByDescending(p => p.Revenue).Take(10).ToList();
        var topByProfit = byProduct.OrderByDescending(p => p.Profit).Take(10).ToList();

        return new ReportsDto(fromUtc, toUtc, summary, daily, topByRevenue, topByProfit);
    }
}
