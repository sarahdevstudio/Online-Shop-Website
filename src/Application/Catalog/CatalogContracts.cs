using Shop.Domain.Catalog;

namespace Shop.Application.Catalog;

public sealed record PagedList<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

// ---- Public read model ----

public sealed record CategoryLinkDto(string Slug, string Name);

public sealed record CategoryTreeDto(string Slug, string Name, IReadOnlyList<CategoryLinkDto> Children);

public sealed record CategoryDetailDto(
    string Slug, string Name, string? Description, CategoryLinkDto? Parent, IReadOnlyList<CategoryLinkDto> Children);

public sealed record BrandDto(string Slug, string Name);

public sealed record ImageDto(string Url, string Alt);

/// <summary>Card in a listing. Price/compare-at are those of the cheapest active variant.</summary>
public sealed record ProductCardDto(
    string Slug, string Name, string? Brand, long Price, long? CompareAtPrice, bool InStock, ImageDto? Image);

public sealed record FacetOptionDto(string Slug, string Value, int Count);

public sealed record AttributeFacetDto(string Code, string Name, string? Unit, IReadOnlyList<FacetOptionDto> Options);

public sealed record PriceRangeDto(long Min, long Max);

/// <summary>
/// Facet counts for one facet ignore that facet's own selection (OR within a facet), but respect every other filter.
/// Options with zero matching products are left out.
/// </summary>
public sealed record ProductFacetsDto(
    IReadOnlyList<FacetOptionDto> Brands, IReadOnlyList<AttributeFacetDto> Attributes, PriceRangeDto? Price);

public sealed record ProductListDto(
    IReadOnlyList<ProductCardDto> Items, int Page, int PageSize, int Total, ProductFacetsDto Facets);

public sealed record VariantDto(
    int Id, string Sku, long Price, long? CompareAtPrice, bool InStock, IReadOnlyDictionary<string, string> Attributes);

public sealed record AxisOptionDto(string Slug, string Value);

public sealed record VariantAxisDto(string Code, string Name, string? Unit, IReadOnlyList<AxisOptionDto> Options);

public sealed record SpecDto(string Code, string Name, string Value, string? Unit);

public sealed record ProductDetailDto(
    string Slug,
    string Name,
    string? Description,
    string Status,
    BrandDto? Brand,
    IReadOnlyList<CategoryLinkDto> Breadcrumb,
    IReadOnlyList<ImageDto> Images,
    IReadOnlyList<VariantAxisDto> Axes,
    IReadOnlyList<VariantDto> Variants,
    IReadOnlyList<SpecDto> Specs,
    DateTime UpdatedAtUtc);

public sealed record SitemapEntryDto(string Slug, DateTime? UpdatedAtUtc);

public sealed record CatalogSitemapDto(IReadOnlyList<SitemapEntryDto> Categories, IReadOnlyList<SitemapEntryDto> Products);

public enum ProductSort
{
    Newest,
    PriceAsc,
    PriceDesc,
}

/// <summary>Parsed listing query. <see cref="Attributes"/>: attribute code → option slugs (OR within a code, AND across codes).</summary>
public sealed record ProductListQuery(
    string? Category = null,
    string? Q = null,
    IReadOnlyList<string>? Brands = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? Attributes = null,
    long? MinPrice = null,
    long? MaxPrice = null,
    bool InStock = false,
    ProductSort Sort = ProductSort.Newest,
    int Page = 1,
    int PageSize = ProductListQuery.DefaultPageSize)
{
    public const int DefaultPageSize = 24;
    public const int MaxPageSize = 60;
    public const int QMaxLength = 100;

    /// <summary>Query-string keys with a fixed meaning; every other key is treated as an attribute code.</summary>
    public static readonly IReadOnlySet<string> ReservedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "category", "q", "brand", "minPrice", "maxPrice", "inStock", "sort", "page", "pageSize",
    };
}

// ---- Admin ----

public sealed record CategoryRequest(string Name, string? Slug, int? ParentId, string? Description, int SortOrder, bool IsActive);

public sealed record AdminCategoryDto(int Id, string Slug, string Name, int? ParentId, string? Description, int SortOrder, bool IsActive);

public sealed record BrandRequest(string Name, string? Slug, bool IsActive);

public sealed record AdminBrandDto(int Id, string Slug, string Name, bool IsActive);

public sealed record AttributeOptionRequest(string Value, string Slug, int SortOrder);

public sealed record AdminAttributeOptionDto(int Id, string Value, string Slug, int SortOrder);

public sealed record AdminAttributeDto(
    int Id, string Code, string Name, string? Unit, AttributeType Type, bool IsFilterable, IReadOnlyList<AdminAttributeOptionDto> Options);

public interface IVariantFields
{
    string Sku { get; }
    long Price { get; }
    long? CompareAtPrice { get; }
    int Stock { get; }
}

public sealed record VariantRequest(string Sku, long Price, long? CompareAtPrice, int Stock, bool IsActive, int SortOrder)
    : IVariantFields;

/// <summary>Update of a variant; <see cref="RowVersion"/> is the value last read, so a concurrent stock change is a 409.</summary>
public sealed record UpdateVariantRequest(
    string Sku, long Price, long? CompareAtPrice, int Stock, bool IsActive, int SortOrder, byte[] RowVersion) : IVariantFields;

/// <summary>Create: slug is set here only. Update: <see cref="Slug"/> and <see cref="Variants"/> are ignored.</summary>
public sealed record ProductRequest(
    int CategoryId, int? BrandId, string Name, string? Slug, string? Description, ProductStatus Status,
    IReadOnlyList<VariantRequest>? Variants);

public sealed record AttributeValueRequest(int AttributeId, int? VariantId, int? OptionId, string? Text);

public sealed record ProductAttributesRequest(IReadOnlyList<AttributeValueRequest> Values);

public sealed record ImageOrderRequest(IReadOnlyList<int> ImageIds);

public sealed record AdminVariantDto(
    int Id, string Sku, long Price, long? CompareAtPrice, int Stock, bool IsActive, int SortOrder, byte[] RowVersion);

public sealed record AdminImageDto(int Id, string Url, string Alt, int SortOrder);

public sealed record AdminAttributeValueDto(int AttributeId, int? VariantId, int? OptionId, string? Text);

public sealed record AdminProductDto(
    int Id,
    string Slug,
    string Name,
    string? Description,
    int CategoryId,
    int? BrandId,
    ProductStatus Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<AdminVariantDto> Variants,
    IReadOnlyList<AdminImageDto> Images,
    IReadOnlyList<AdminAttributeValueDto> Attributes);

public sealed record AdminProductListItemDto(
    int Id, string Slug, string Name, ProductStatus Status, string Category, string? Brand,
    int VariantCount, long? MinPrice, int TotalStock, DateTime UpdatedAtUtc);

public sealed record ImageUpload(Stream Content, string FileName, string ContentType, long Length, string? Alt);
