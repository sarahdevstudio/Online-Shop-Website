using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using Shop.Application.Admin;
using Shop.Application.Common;
using Shop.Domain.Catalog;
using Shop.Domain.Inventory;
using Shop.Domain.Orders;
using Shop.Domain.Reviews;
using Shop.Domain.Security;
using Shop.Infrastructure.Persistence;
using Shop.Domain.Common;

namespace Shop.Infrastructure.Catalog;

internal sealed class StockAdminService(ShopDbContext db, ICurrentUser currentUser, TimeProvider clock) : IStockAdminService
{
    private const int MaxDelta = 1_000_000;

    public async Task<Result<StockAdjustResultDto>> AdjustAsync(int variantId, StockAdjustRequest request, CancellationToken ct)
    {
        if (request.Delta == 0 || Math.Abs((long)request.Delta) > MaxDelta)
        {
            return Error.Validation(new Dictionary<string, string[]> { ["delta"] = [Msg.Of("api.validation.stock-delta", ("max", MaxDelta))] });
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // One statement does the arithmetic and the bounds check, so concurrent checkouts / adjustments serialise on the
        // row lock and nothing is lost or driven below zero; OUTPUT returns the resulting stock and row version.
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = tx.GetDbTransaction();
        command.CommandText = """
            UPDATE ProductVariants SET Stock = Stock + @delta
            OUTPUT inserted.Stock, inserted.RowVersion
            WHERE Id = @id AND Stock + @delta >= 0 AND Stock + @delta <= @max
            """;
        AddParameter(command, "@delta", request.Delta);
        AddParameter(command, "@id", variantId);
        AddParameter(command, "@max", ProductVariant.MaxStock);

        int stockAfter;
        byte[] rowVersion;
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct))
            {
                await reader.CloseAsync();
                return await db.ProductVariants.AnyAsync(v => v.Id == variantId, ct)
                    ? Error.Conflict("stock_out_of_range", "api.validation.stock-range")
                    : Error.NotFound("variant_not_found", "api.error.variant-not-found");
            }

            stockAfter = reader.GetInt32(0);
            rowVersion = (byte[])reader.GetValue(1);
        }

        db.StockAdjustments.Add(StockAdjustment.Create(
            variantId, currentUser.UserId, request.Delta, stockAfter, request.Reason, StockAdjustment.AdjustSource, clock.GetUtcNow().UtcDateTime));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new StockAdjustResultDto(variantId, stockAfter, rowVersion);
    }

    public async Task<Result<IReadOnlyList<StockAdjustmentDto>>> HistoryAsync(int variantId, CancellationToken ct)
    {
        if (!await db.ProductVariants.AnyAsync(v => v.Id == variantId, ct))
        {
            return Error.NotFound("variant_not_found", "api.error.variant-not-found");
        }

        var rows = await (
                from a in db.StockAdjustments.AsNoTracking()
                where a.VariantId == variantId
                join u in db.Users.AsNoTracking() on a.AdminUserId equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby a.AtUtc descending, a.Id descending
                select new StockAdjustmentDto(a.Id, a.AtUtc, a.AdminUserId, u.UserName, a.Delta, a.StockAfter, a.Reason, a.Source))
            .Take(30)
            .ToListAsync(ct);
        return rows;
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}

internal sealed class DashboardService(ShopDbContext db, TimeProvider clock, ICurrentUser currentUser, IReportsService reports) : IDashboardService
{
    public const int LowStockThreshold = 3;

    public async Task<DashboardDto> GetAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var todayStartUtc = TehranTime.LocalMidnightUtc(now);

        DashboardOrdersDto? orders = null;
        if (currentUser.HasPermission(Permissions.OrdersView))
        {
            var newOrders = await db.Orders.CountAsync(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.AwaitingPayment, ct);
            var waiting = await db.Orders.CountAsync(o => o.Status == OrderStatus.AwaitingPayment && o.TransferTracking != null, ct);
            var toShip = await db.Orders.CountAsync(o => o.Status == OrderStatus.Confirmed, ct);
            var today = await db.Orders.CountAsync(o => o.CreatedAtUtc >= todayStartUtc, ct);
            orders = new DashboardOrdersDto(newOrders, waiting, toShip, today);
        }

        DashboardReportsDto? reportsCard = null;
        if (currentUser.HasPermission(Permissions.ReportsView))
        {
            var monthStartUtc = TehranTime.LocalMidnightUtc(new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TehranTime.Offset));
            var todayReport = await reports.GetAsync(todayStartUtc, todayStartUtc.AddDays(1), ct);
            var monthReport = await reports.GetAsync(monthStartUtc, todayStartUtc.AddDays(1), ct);
            var byDate = monthReport.Daily.ToDictionary(d => d.Date, d => d.Revenue);
            var sparkline = Enumerable.Range(0, 30)
                .Select(i => TehranTime.LocalDate(todayStartUtc.AddDays(-29 + i)))
                .Select(date => byDate.GetValueOrDefault(date))
                .ToList();
            reportsCard = new DashboardReportsDto(
                todayReport.Summary.Revenue, todayReport.Summary.GrossProfit,
                monthReport.Summary.Revenue, monthReport.Summary.GrossProfit, sparkline);
        }

        int? pendingReviews = currentUser.HasPermission(Permissions.ReviewsView)
            ? await db.ProductReviews.CountAsync(r => r.Status == ReviewStatus.Pending, ct)
            : null;

        DashboardLowStockDto? lowStock = null;
        if (currentUser.HasPermission(Permissions.CatalogView))
        {
            var low = await db.ProductVariants.CountAsync(v =>
                v.IsActive && v.Stock <= LowStockThreshold && db.Products.Any(p => p.Id == v.ProductId && p.Status == ProductStatus.Published), ct);
            lowStock = new DashboardLowStockDto(low, LowStockThreshold);
        }

        int? draftProducts = currentUser.HasPermission(Permissions.CatalogView)
            ? await db.Products.CountAsync(p => p.Status == ProductStatus.Draft, ct)
            : null;

        return new DashboardDto(orders, reportsCard, pendingReviews, lowStock, draftProducts);
    }
}
