using Microsoft.EntityFrameworkCore;
using Shop.Application.Catalog;
using Shop.Infrastructure.Persistence;

namespace Shop.Infrastructure.Catalog;

internal sealed class StockService(ShopDbContext db) : IStockService
{
    public async Task<bool> TryDecrementAsync(int variantId, int quantity, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        // One conditional UPDATE: the row lock serialises concurrent buyers, and the WHERE re-checks the stock each
        // one sees, so exactly the available quantity succeeds. CK_ProductVariants_Stock is the last line of defence.
        var updated = await db.ProductVariants
            .Where(v => v.Id == variantId && v.IsActive && v.Stock >= quantity)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.Stock, v => v.Stock - quantity), ct);
        return updated == 1;
    }

    public async Task IncrementAsync(int variantId, int quantity, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        var updated = await db.ProductVariants
            .Where(v => v.Id == variantId)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.Stock, v => v.Stock + quantity), ct);
        if (updated != 1)
        {
            throw new InvalidOperationException($"Variant {variantId} does not exist.");
        }
    }
}
