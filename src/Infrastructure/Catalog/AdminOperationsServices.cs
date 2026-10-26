using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;
using Shop.Application.Admin;
using Shop.Application.Common;
using Shop.Domain.Catalog;
using Shop.Domain.Inventory;
using Shop.Domain.Orders;
using Shop.Infrastructure.Persistence;

namespace Shop.Infrastructure.Catalog;

internal sealed class StockAdminService(ShopDbContext db, ICurrentUser currentUser, TimeProvider clock) : IStockAdminService
{
    private const int MaxDelta = 1_000_000;

    public async Task<Result<StockAdjustResultDto>> AdjustAsync(int variantId, StockAdjustRequest request, CancellationToken ct)
    {
        if (request.Delta == 0 || Math.Abs((long)request.Delta) > MaxDelta)
        {
            return Error.Validation(new Dictionary<string, string[]> { ["delta"] = [$"The change must be a non-zero whole number up to {MaxDelta}."] });
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
                    ? Error.Conflict("stock_out_of_range", "The stock can't go below zero (or above the maximum).")
                    : Error.NotFound("variant_not_found", "Variant was not found.");
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
            return Error.NotFound("variant_not_found", "Variant was not found.");
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

internal sealed class DashboardService(ShopDbContext db, TimeProvider clock) : IDashboardService
{
    public const int LowStockThreshold = 3;

    // Iran has had no daylight saving since 2022: "today" starts at midnight at UTC+03:30.
    private static readonly TimeSpan TehranOffset = TimeSpan.FromMinutes(210);

    public async Task<DashboardDto> GetAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var localMidnight = new DateTimeOffset(now.ToOffset(TehranOffset).Date, TehranOffset).UtcDateTime;

        var newOrders = await db.Orders.CountAsync(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.AwaitingPayment, ct);
        var waiting = await db.Orders.CountAsync(o => o.Status == OrderStatus.AwaitingPayment && o.TransferTracking != null, ct);
        var toShip = await db.Orders.CountAsync(o => o.Status == OrderStatus.Confirmed, ct);
        var today = await db.Orders.CountAsync(o => o.CreatedAtUtc >= localMidnight, ct);
        var low = await db.ProductVariants.CountAsync(v =>
            v.IsActive && v.Stock <= LowStockThreshold && db.Products.Any(p => p.Id == v.ProductId && p.Status == ProductStatus.Published), ct);
        return new DashboardDto(newOrders, waiting, toShip, today, low, LowStockThreshold);
    }
}
