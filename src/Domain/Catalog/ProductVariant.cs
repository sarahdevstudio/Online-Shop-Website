using System.Text.RegularExpressions;

namespace Shop.Domain.Catalog;

/// <summary>
/// A purchasable combination (e.g. 9W / 3000K). Prices are whole Toman. <see cref="Stock"/> is never edited by
/// checkout code directly: it goes through a conditional update so it can't drop below zero.
/// </summary>
public sealed partial class ProductVariant
{
    public const int SkuMaxLength = 64;
    public const long MaxPrice = 1_000_000_000_000;
    public const int MaxStock = 1_000_000;

    private ProductVariant()
    {
        Sku = string.Empty;
    }

    public int Id { get; private set; }
    public int ProductId { get; private set; }
    public string Sku { get; private set; }
    public long Price { get; private set; }
    public long? CompareAtPrice { get; private set; }
    public int Stock { get; private set; }
    public bool IsActive { get; private set; }
    public int SortOrder { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static bool IsValidSku(string? sku) => sku is { Length: > 0 and <= SkuMaxLength } && SkuPattern().IsMatch(sku);

    public static ProductVariant Create(string sku, long price, long? compareAtPrice, int stock, bool isActive, int sortOrder)
    {
        var variant = new ProductVariant();
        variant.Update(sku, price, compareAtPrice, stock, isActive, sortOrder);
        return variant;
    }

    public void Update(string sku, long price, long? compareAtPrice, int stock, bool isActive, int sortOrder)
    {
        if (!IsValidSku(sku))
        {
            throw new ArgumentException($"'{sku}' is not a valid SKU.", nameof(sku));
        }

        if (price is <= 0 or > MaxPrice)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Price must be a positive whole Toman amount.");
        }

        if (compareAtPrice is not null && (compareAtPrice <= price || compareAtPrice > MaxPrice))
        {
            throw new ArgumentOutOfRangeException(nameof(compareAtPrice), "Compare-at price must be higher than the price.");
        }

        if (stock is < 0 or > MaxStock)
        {
            throw new ArgumentOutOfRangeException(nameof(stock));
        }

        Sku = sku;
        Price = price;
        CompareAtPrice = compareAtPrice;
        Stock = stock;
        IsActive = isActive;
        SortOrder = sortOrder;
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SkuPattern();
}
