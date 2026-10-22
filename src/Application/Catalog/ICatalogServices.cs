using Shop.Application.Common;

namespace Shop.Application.Catalog;

/// <summary>Anonymous storefront reads. Only active categories/brands and Published (listings) or Published/Archived (detail) products.</summary>
public interface ICatalogService
{
    Task<IReadOnlyList<CategoryTreeDto>> GetCategoryTreeAsync(CancellationToken ct);

    Task<Result<CategoryDetailDto>> GetCategoryAsync(string slug, CancellationToken ct);

    Task<IReadOnlyList<BrandDto>> GetBrandsAsync(CancellationToken ct);

    Task<Result<ProductListDto>> GetProductsAsync(ProductListQuery query, CancellationToken ct);

    Task<Result<ProductDetailDto>> GetProductAsync(string slug, CancellationToken ct);

    Task<CatalogSitemapDto> GetSitemapAsync(CancellationToken ct);
}

public interface ICatalogAdminService
{
    Task<IReadOnlyList<AdminCategoryDto>> GetCategoriesAsync(CancellationToken ct);
    Task<Result<AdminCategoryDto>> CreateCategoryAsync(CategoryRequest request, CancellationToken ct);
    Task<Result<AdminCategoryDto>> UpdateCategoryAsync(int id, CategoryRequest request, CancellationToken ct);
    Task<Result> DeleteCategoryAsync(int id, CancellationToken ct);

    Task<IReadOnlyList<AdminBrandDto>> GetBrandsAsync(CancellationToken ct);
    Task<Result<AdminBrandDto>> CreateBrandAsync(BrandRequest request, CancellationToken ct);
    Task<Result<AdminBrandDto>> UpdateBrandAsync(int id, BrandRequest request, CancellationToken ct);
    Task<Result> DeleteBrandAsync(int id, CancellationToken ct);

    Task<IReadOnlyList<AdminAttributeDto>> GetAttributesAsync(CancellationToken ct);
    Task<Result<AdminAttributeOptionDto>> AddAttributeOptionAsync(int attributeId, AttributeOptionRequest request, CancellationToken ct);

    Task<PagedList<AdminProductListItemDto>> GetProductsAsync(string? q, Domain.Catalog.ProductStatus? status, int page, CancellationToken ct);
    Task<Result<AdminProductDto>> GetProductAsync(int id, CancellationToken ct);
    Task<Result<AdminProductDto>> CreateProductAsync(ProductRequest request, CancellationToken ct);
    Task<Result<AdminProductDto>> UpdateProductAsync(int id, ProductRequest request, CancellationToken ct);

    Task<Result<AdminVariantDto>> AddVariantAsync(int productId, VariantRequest request, CancellationToken ct);
    Task<Result<AdminVariantDto>> UpdateVariantAsync(int variantId, UpdateVariantRequest request, CancellationToken ct);

    /// <summary>Replaces the product's whole attribute value set.</summary>
    Task<Result<AdminProductDto>> SetAttributesAsync(int productId, ProductAttributesRequest request, CancellationToken ct);

    Task<Result<AdminImageDto>> AddImageAsync(int productId, ImageUpload upload, CancellationToken ct);
    Task<Result<AdminProductDto>> ReorderImagesAsync(int productId, ImageOrderRequest request, CancellationToken ct);
    Task<Result> DeleteImageAsync(int imageId, CancellationToken ct);
}

/// <summary>
/// Stock changes that must never oversell. Checkout calls <see cref="TryDecrementAsync"/> inside its order transaction;
/// cancelling an order calls <see cref="IncrementAsync"/>.
/// </summary>
public interface IStockService
{
    /// <summary>Atomically takes <paramref name="quantity"/> from the variant; false (nothing changed) when not enough is left.</summary>
    Task<bool> TryDecrementAsync(int variantId, int quantity, CancellationToken ct);

    Task IncrementAsync(int variantId, int quantity, CancellationToken ct);
}

/// <summary>Stores uploaded files under the media root; paths are relative and use forward slashes.</summary>
public interface IMediaStorage
{
    Task<string> SaveAsync(Stream content, string folder, string extension, CancellationToken ct);

    Task DeleteAsync(string path, CancellationToken ct);

    /// <summary>Public URL path for a stored file (served by the API under /media).</summary>
    string GetUrl(string path);
}
