using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Shop.Application.Catalog;
using Shop.Application.Orders;
using Shop.Domain.Catalog;
using Shop.Infrastructure.Persistence;

namespace Shop.Infrastructure.Orders;

/// <summary>Random, unguessable tokens (32 bytes, base64url = 43 characters) for carts and the guest order page.</summary>
internal static class Tokens
{
    public const int Length = 43;

    public static string New() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public static bool LooksValid(string? token) =>
        token is { Length: Length } && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    /// <summary>Carts are stored by hash, so a database leak does not hand out live cart tokens.</summary>
    public static byte[] Hash(string token) => SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(token));
}

/// <summary>A variant as the cart and checkout need it: live price and stock, and whether it can be bought at all.</summary>
internal sealed record VariantInfo(
    int VariantId, string ProductName, string ProductSlug, string Sku, long Price, int Stock, bool Purchasable,
    string? Label, string? ImagePath)
{
    /// <summary>The most of this variant a customer may hold: the stock, capped by the configured per-line maximum.</summary>
    public int MaxQuantity(CheckoutOptions options) => Purchasable ? Math.Min(Stock, options.MaxQuantityPerLine) : 0;
}

internal static class VariantInfoLoader
{
    public static async Task<Dictionary<int, VariantInfo>> LoadAsync(ShopDbContext db, IReadOnlyCollection<int> variantIds, CancellationToken ct)
    {
        if (variantIds.Count == 0)
        {
            return [];
        }

        var rows = await (
                from v in db.ProductVariants.AsNoTracking()
                join p in db.Products.AsNoTracking() on v.ProductId equals p.Id
                where variantIds.Contains(v.Id)
                select new { v.Id, v.ProductId, p.Name, p.Slug, v.Sku, v.Price, v.Stock, v.IsActive, p.Status })
            .ToListAsync(ct);

        // Variant-level option values ("۹ وات", "آفتابی") joined into a short label, in attribute order.
        var labelRows = await (
                from av in db.ProductAttributeValues.AsNoTracking()
                join o in db.AttributeOptions.AsNoTracking() on av.OptionId equals o.Id
                join a in db.Attributes.AsNoTracking() on av.AttributeId equals a.Id
                where av.VariantId != null && variantIds.Contains(av.VariantId.Value)
                orderby a.SortOrder, a.Id
                select new { VariantId = av.VariantId!.Value, o.Value })
            .ToListAsync(ct);
        var labels = labelRows.GroupBy(r => r.VariantId).ToDictionary(g => g.Key, g => string.Join(" / ", g.Select(r => r.Value)));

        var productIds = rows.Select(r => r.ProductId).Distinct().ToList();
        var imageRows = await db.ProductImages.AsNoTracking()
            .Where(i => productIds.Contains(i.ProductId))
            .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
            .Select(i => new { i.ProductId, i.Path })
            .ToListAsync(ct);
        var firstImage = imageRows.GroupBy(i => i.ProductId).ToDictionary(g => g.Key, g => g.First().Path);

        return rows.ToDictionary(
            r => r.Id,
            r => new VariantInfo(
                r.Id, r.Name, r.Slug, r.Sku, r.Price, r.Stock,
                r.IsActive && r.Status == ProductStatus.Published && r.Stock > 0,
                labels.GetValueOrDefault(r.Id),
                firstImage.GetValueOrDefault(r.ProductId)));
    }
}

internal static class CartStore
{
    public static DateTime Cutoff(TimeProvider clock) => clock.GetUtcNow().UtcDateTime.AddDays(-Shop.Domain.Orders.Cart.ExpiryDays);

    /// <summary>The live (not expired) cart for a cookie token, tracked, with its lines; null when there is none.</summary>
    public static async Task<Shop.Domain.Orders.Cart?> FindAsync(ShopDbContext db, string? token, TimeProvider clock, CancellationToken ct)
    {
        if (!Tokens.LooksValid(token))
        {
            return null;
        }

        var hash = Tokens.Hash(token!);
        var cutoff = Cutoff(clock);
        return await db.Carts.Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.TokenHash == hash && c.LastActivityAtUtc > cutoff, ct);
    }
}
