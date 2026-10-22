using Shop.Domain.Common;

namespace Shop.Domain.Catalog;

public enum ProductStatus
{
    /// <summary>Not visible to customers (404).</summary>
    Draft = 0,

    /// <summary>Listed and purchasable.</summary>
    Published = 1,

    /// <summary>Page stays reachable as "unavailable"; excluded from listings and the sitemap.</summary>
    Archived = 2,
}

/// <summary>
/// One sellable model. Every product has at least one <see cref="ProductVariant"/>; prices and stock live on the variants.
/// </summary>
public sealed class Product
{
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 10_000;
    public const int SearchKeyMaxLength = 400;

    private Product()
    {
        Name = Slug = SearchKey = string.Empty;
    }

    public int Id { get; private set; }
    public int CategoryId { get; private set; }
    public int? BrandId { get; private set; }
    public string Name { get; private set; }
    public string Slug { get; private set; }

    /// <summary><see cref="PersianText.SearchKey"/> of name + brand name; what <c>q</c> is matched against.</summary>
    public string SearchKey { get; private set; }

    public string? Description { get; private set; }
    public ProductStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public List<ProductVariant> Variants { get; private set; } = [];
    public List<ProductImage> Images { get; private set; } = [];
    public List<ProductAttributeValue> AttributeValues { get; private set; } = [];

    public static Product Create(
        int categoryId, int? brandId, string? brandName, string name, string slug, string? description,
        ProductStatus status, DateTime utcNow)
    {
        var product = new Product { Slug = Catalog.Slug.Require(slug, nameof(slug)), CreatedAtUtc = utcNow };
        product.Update(categoryId, brandId, brandName, name, description, status, utcNow);
        return product;
    }

    public void Update(
        int categoryId, int? brandId, string? brandName, string name, string? description, ProductStatus status, DateTime utcNow)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        CategoryId = categoryId;
        BrandId = brandId;
        Name = CatalogText.RequireName(name, NameMaxLength, nameof(name));
        Description = CatalogText.OptionalDescription(description, DescriptionMaxLength, nameof(description));
        Status = status;
        UpdatedAtUtc = utcNow;
        RefreshSearchKey(brandName);
    }

    /// <summary>Called when the brand is renamed, since the key includes the brand name.</summary>
    public void RefreshSearchKey(string? brandName)
    {
        var key = PersianText.SearchKey($"{Name} {brandName}");
        SearchKey = key.Length <= SearchKeyMaxLength ? key : key[..SearchKeyMaxLength];
    }

    public void Touch(DateTime utcNow) => UpdatedAtUtc = utcNow;
}
