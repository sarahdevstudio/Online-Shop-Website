using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shop.Application.Catalog;
using Shop.Application.Common;
using Shop.Domain.Catalog;
using Shop.Domain.Common;
using Shop.Domain.Inventory;
using Shop.Infrastructure.Persistence;

namespace Shop.Infrastructure.Catalog;

internal sealed class CatalogAdminService(
    ShopDbContext db, IMediaStorage media, IOptions<MediaOptions> mediaOptions, ICurrentUser currentUser, TimeProvider clock) : ICatalogAdminService
{
    public const int AdminPageSize = 50;

    private static readonly Error SlugTaken = Error.Conflict("slug_taken", "This slug is already in use.");
    private static readonly Error SkuTaken = Error.Conflict("sku_taken", "This SKU is already in use.");

    // ---- categories ----

    public async Task<IReadOnlyList<AdminCategoryDto>> GetCategoriesAsync(CancellationToken ct) =>
        await db.Categories.AsNoTracking()
            .OrderBy(c => c.ParentId ?? c.Id).ThenBy(c => c.ParentId != null).ThenBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => ToDto(c))
            .ToListAsync(ct);

    public async Task<Result<AdminCategoryDto>> CreateCategoryAsync(CategoryRequest request, CancellationToken ct)
    {
        if (request.Slug is null)
        {
            return Invalid("slug", "Required.");
        }

        if (await ValidateParentAsync(null, request.ParentId, ct) is { } parentError)
        {
            return parentError;
        }

        if (await db.Categories.AnyAsync(c => c.Slug == request.Slug, ct))
        {
            return SlugTaken;
        }

        var category = Category.Create(request.Name, request.Slug, request.ParentId, request.Description, request.SortOrder, request.IsActive);
        db.Categories.Add(category);
        return await SaveAsync(() => ToDto(category), ("UX_Categories_Slug", SlugTaken), ct);
    }

    public async Task<Result<AdminCategoryDto>> UpdateCategoryAsync(int id, CategoryRequest request, CancellationToken ct)
    {
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (category is null)
        {
            return NotFound("category");
        }

        if (await ValidateParentAsync(id, request.ParentId, ct) is { } parentError)
        {
            return parentError;
        }

        category.Update(request.Name, request.ParentId, request.Description, request.SortOrder, request.IsActive);
        await db.SaveChangesAsync(ct);
        return ToDto(category);
    }

    public async Task<Result> DeleteCategoryAsync(int id, CancellationToken ct)
    {
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id, ct);
        if (category is null)
        {
            return Result.Failure(NotFound("category"));
        }

        if (await db.Categories.AnyAsync(c => c.ParentId == id, ct) || await db.Products.AnyAsync(p => p.CategoryId == id, ct))
        {
            return Result.Failure(Error.Conflict("category_not_empty", "The category still has subcategories or products."));
        }

        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <summary>Two levels at most: the parent must be a root, and a category with children can't get a parent.</summary>
    private async Task<Error?> ValidateParentAsync(int? categoryId, int? parentId, CancellationToken ct)
    {
        if (parentId is null)
        {
            return null;
        }

        if (parentId == categoryId)
        {
            return Invalid("parentId", "A category can't be its own parent.");
        }

        var parent = await db.Categories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == parentId, ct);
        if (parent is null)
        {
            return Invalid("parentId", "Parent category does not exist.");
        }

        if (parent.ParentId is not null)
        {
            return Invalid("parentId", "Categories can be at most two levels deep: the parent must be a top-level category.");
        }

        if (categoryId is not null && await db.Categories.AnyAsync(c => c.ParentId == categoryId, ct))
        {
            return Invalid("parentId", "A category with subcategories can't be placed under another category.");
        }

        return null;
    }

    // ---- brands ----

    public async Task<IReadOnlyList<AdminBrandDto>> GetBrandsAsync(CancellationToken ct) =>
        await db.Brands.AsNoTracking().OrderBy(b => b.Name).Select(b => ToDto(b)).ToListAsync(ct);

    public async Task<Result<AdminBrandDto>> CreateBrandAsync(BrandRequest request, CancellationToken ct)
    {
        if (request.Slug is null)
        {
            return Invalid("slug", "Required.");
        }

        var name = PersianText.Normalize(request.Name);
        if (await db.Brands.AnyAsync(b => b.Slug == request.Slug, ct))
        {
            return SlugTaken;
        }

        if (await db.Brands.AnyAsync(b => b.Name == name, ct))
        {
            return BrandNameTaken;
        }

        var brand = Brand.Create(request.Name, request.Slug, request.IsActive);
        db.Brands.Add(brand);
        return await SaveAsync(() => ToDto(brand), [("UX_Brands_Slug", SlugTaken), ("UX_Brands_Name", BrandNameTaken)], ct);
    }

    public async Task<Result<AdminBrandDto>> UpdateBrandAsync(int id, BrandRequest request, CancellationToken ct)
    {
        var brand = await db.Brands.SingleOrDefaultAsync(b => b.Id == id, ct);
        if (brand is null)
        {
            return NotFound("brand");
        }

        var name = PersianText.Normalize(request.Name);
        if (await db.Brands.AnyAsync(b => b.Id != id && b.Name == name, ct))
        {
            return BrandNameTaken;
        }

        var renamed = brand.Name != name;
        brand.Update(request.Name, request.IsActive);
        if (renamed)
        {
            // The brand name is part of each product's search key.
            foreach (var product in await db.Products.Where(p => p.BrandId == id).ToListAsync(ct))
            {
                product.RefreshSearchKey(brand.Name);
            }
        }

        return await SaveAsync(() => ToDto(brand), ("UX_Brands_Name", BrandNameTaken), ct);
    }

    public async Task<Result> DeleteBrandAsync(int id, CancellationToken ct)
    {
        var brand = await db.Brands.SingleOrDefaultAsync(b => b.Id == id, ct);
        if (brand is null)
        {
            return Result.Failure(NotFound("brand"));
        }

        if (await db.Products.AnyAsync(p => p.BrandId == id, ct))
        {
            return Result.Failure(Error.Conflict("brand_in_use", "Products still use this brand."));
        }

        db.Brands.Remove(brand);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static Error BrandNameTaken => Error.Conflict("name_taken", "A brand with this name already exists.");

    // ---- attributes ----

    public async Task<IReadOnlyList<AdminAttributeDto>> GetAttributesAsync(CancellationToken ct) =>
        (await db.Attributes.AsNoTracking().Include(a => a.Options).OrderBy(a => a.SortOrder).ToListAsync(ct))
            .Select(a => new AdminAttributeDto(a.Id, a.Code, a.Name, a.Unit, a.Type, a.IsFilterable,
                a.Options.OrderBy(o => o.SortOrder).Select(ToDto).ToList()))
            .ToList();

    public async Task<Result<AdminAttributeOptionDto>> AddAttributeOptionAsync(int attributeId, AttributeOptionRequest request, CancellationToken ct)
    {
        var attribute = await db.Attributes.Include(a => a.Options).SingleOrDefaultAsync(a => a.Id == attributeId, ct);
        if (attribute is null)
        {
            return NotFound("attribute");
        }

        if (attribute.Type != AttributeType.Option)
        {
            return Invalid("attributeId", "Only option attributes have options.");
        }

        var taken = Error.Conflict("option_slug_taken", "This attribute already has an option with this slug.");
        if (attribute.Options.Any(o => o.Slug == request.Slug))
        {
            return taken;
        }

        var option = AttributeOption.Create(request.Value, request.Slug, request.SortOrder);
        attribute.Options.Add(option);
        return await SaveAsync(() => ToDto(option), ("UX_AttributeOptions_AttributeId_Slug", taken), ct);
    }

    // ---- products ----

    public async Task<PagedList<AdminProductListItemDto>> GetProductsAsync(string? q, ProductStatus? status, int page, CancellationToken ct)
    {
        var query = db.Products.AsNoTracking();
        foreach (var token in PersianText.SearchKey(q).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            query = query.Where(p => p.SearchKey.Contains(token) || p.Slug.Contains(token));
        }

        if (status is not null)
        {
            query = query.Where(p => p.Status == status);
        }

        page = Math.Max(page, 1);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(p => p.UpdatedAtUtc).ThenByDescending(p => p.Id)
            .Skip((page - 1) * AdminPageSize).Take(AdminPageSize)
            .Select(p => new AdminProductListItemDto(
                p.Id, p.Slug, p.Name, p.Status,
                db.Categories.Where(c => c.Id == p.CategoryId).Select(c => c.Name).First(),
                db.Brands.Where(b => b.Id == p.BrandId).Select(b => b.Name).FirstOrDefault(),
                p.Variants.Count,
                p.Variants.Where(v => v.IsActive).Min(v => (long?)v.Price),
                p.Variants.Sum(v => v.Stock),
                p.UpdatedAtUtc))
            .ToListAsync(ct);
        return new PagedList<AdminProductListItemDto>(items, page, AdminPageSize, total);
    }

    public async Task<Result<AdminProductDto>> GetProductAsync(int id, CancellationToken ct)
    {
        var product = await LoadProductAsync(id, tracking: false, ct);
        return product is null ? NotFound("product") : ToDto(product);
    }

    public async Task<Result<AdminProductDto>> CreateProductAsync(ProductRequest request, CancellationToken ct)
    {
        if (request.Slug is null)
        {
            return Invalid("slug", "Required.");
        }

        if (request.Variants is not { Count: > 0 } variants)
        {
            return Invalid("variants", "At least one variant is required.");
        }

        var skus = variants.Select(v => v.Sku).ToList();
        if (skus.Distinct(StringComparer.OrdinalIgnoreCase).Count() != skus.Count)
        {
            return Invalid("variants", "SKUs must be unique.");
        }

        var references = await ValidateReferencesAsync(request, ct);
        if (references.Error is { } error)
        {
            return error;
        }

        if (await db.Products.AnyAsync(p => p.Slug == request.Slug, ct))
        {
            return SlugTaken;
        }

        if (await db.ProductVariants.AnyAsync(v => skus.Contains(v.Sku), ct))
        {
            return SkuTaken;
        }

        var product = Product.Create(request.CategoryId, request.BrandId, references.BrandName, request.Name, request.Slug,
            request.Description, request.Status, clock.GetUtcNow().UtcDateTime);
        product.Variants.AddRange(variants.Select(v => ProductVariant.Create(v.Sku, v.Price, v.CompareAtPrice, v.Stock, v.IsActive, v.SortOrder)));
        db.Products.Add(product);
        return await SaveAsync(() => ToDto(product), [("UX_Products_Slug", SlugTaken), ("UX_ProductVariants_Sku", SkuTaken)], ct);
    }

    public async Task<Result<AdminProductDto>> UpdateProductAsync(int id, ProductRequest request, CancellationToken ct)
    {
        var product = await LoadProductAsync(id, tracking: true, ct);
        if (product is null)
        {
            return NotFound("product");
        }

        var references = await ValidateReferencesAsync(request, ct);
        if (references.Error is { } error)
        {
            return error;
        }

        product.Update(request.CategoryId, request.BrandId, references.BrandName, request.Name, request.Description,
            request.Status, clock.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
        return ToDto(product);
    }

    public async Task<Result<AdminVariantDto>> AddVariantAsync(int productId, VariantRequest request, CancellationToken ct)
    {
        var product = await db.Products.SingleOrDefaultAsync(p => p.Id == productId, ct);
        if (product is null)
        {
            return NotFound("product");
        }

        if (await db.ProductVariants.AnyAsync(v => v.Sku == request.Sku, ct))
        {
            return SkuTaken;
        }

        var variant = ProductVariant.Create(request.Sku, request.Price, request.CompareAtPrice, request.Stock, request.IsActive, request.SortOrder);
        product.Variants.Add(variant);
        product.Touch(clock.GetUtcNow().UtcDateTime);
        return await SaveAsync(() => ToDto(variant), ("UX_ProductVariants_Sku", SkuTaken), ct);
    }

    public async Task<Result<AdminVariantDto>> UpdateVariantAsync(int variantId, UpdateVariantRequest request, CancellationToken ct)
    {
        var variant = await db.ProductVariants.SingleOrDefaultAsync(v => v.Id == variantId, ct);
        if (variant is null)
        {
            return NotFound("variant");
        }

        if (await db.ProductVariants.AnyAsync(v => v.Id != variantId && v.Sku == request.Sku, ct))
        {
            return SkuTaken;
        }

        // Compare against the version the admin read: a checkout that changed the stock meanwhile makes this a 409
        // (DbUpdateConcurrencyException → global handler) instead of silently overwriting the stock.
        db.Entry(variant).Property(v => v.RowVersion).OriginalValue = request.RowVersion;
        var oldStock = variant.Stock;
        variant.Update(request.Sku, request.Price, request.CompareAtPrice, request.Stock, request.IsActive, request.SortOrder);
        if (request.Stock != oldStock)
        {
            db.StockAdjustments.Add(StockAdjustment.Create(
                variantId, currentUser.UserId, request.Stock - oldStock, request.Stock, null, StockAdjustment.EditSource, clock.GetUtcNow().UtcDateTime));
        }

        await db.Products.Where(p => p.Id == variant.ProductId).ForEachAsync(p => p.Touch(clock.GetUtcNow().UtcDateTime), ct);
        return await SaveAsync(() => ToDto(variant), ("UX_ProductVariants_Sku", SkuTaken), ct);
    }

    public async Task<Result<AdminProductDto>> SetAttributesAsync(int productId, ProductAttributesRequest request, CancellationToken ct)
    {
        var product = await LoadProductAsync(productId, tracking: true, ct);
        if (product is null)
        {
            return NotFound("product");
        }

        var attributeIds = request.Values.Select(v => v.AttributeId).Distinct().ToList();
        var attributes = await db.Attributes.AsNoTracking().Include(a => a.Options)
            .Where(a => attributeIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        var variantIds = product.Variants.Select(v => v.Id).ToHashSet();

        var errors = new Dictionary<string, string[]>();
        for (var i = 0; i < request.Values.Count; i++)
        {
            var value = request.Values[i];
            var message = !attributes.TryGetValue(value.AttributeId, out var attribute) ? "Attribute does not exist."
                : value.VariantId is { } variantId && !variantIds.Contains(variantId) ? "Variant does not belong to this product."
                : attribute.Type == AttributeType.Option && (value.OptionId is null || attribute.Options.All(o => o.Id != value.OptionId))
                    ? "An option of this attribute is required."
                : attribute.Type == AttributeType.Text && value.Text is null ? "Text is required for this attribute."
                : null;
            if (message is not null)
            {
                errors[$"values[{i}]"] = [message];
            }
        }

        if (request.Values.GroupBy(v => (v.AttributeId, v.VariantId)).Any(g => g.Count() > 1))
        {
            errors["values"] = ["Each attribute may appear once per product and once per variant."];
        }
        else if (request.Values.GroupBy(v => v.AttributeId).Any(g => g.Any(v => v.VariantId is null) && g.Any(v => v.VariantId is not null)))
        {
            errors["values"] = ["An attribute is either a product spec or a variant axis, not both."];
        }

        if (errors.Count > 0)
        {
            return Error.Validation(errors);
        }

        product.AttributeValues.Clear();
        product.AttributeValues.AddRange(request.Values.Select(v => v.OptionId is { } optionId
            ? ProductAttributeValue.ForOption(v.AttributeId, optionId, v.VariantId)
            : ProductAttributeValue.ForText(v.AttributeId, v.Text!, v.VariantId)));
        product.Touch(clock.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
        return ToDto(product);
    }

    // ---- images ----

    public async Task<Result<AdminImageDto>> AddImageAsync(int productId, ImageUpload upload, CancellationToken ct)
    {
        var product = await db.Products.Include(p => p.Images).SingleOrDefaultAsync(p => p.Id == productId, ct);
        if (product is null)
        {
            return NotFound("product");
        }

        var maxBytes = mediaOptions.Value.MaxImageBytes;
        if (upload.Length is <= 0 || upload.Length > maxBytes)
        {
            return Invalid("file", $"Image must be between 1 byte and {maxBytes} bytes.");
        }

        using var buffer = new MemoryStream();
        await upload.Content.CopyToAsync(buffer, ct);
        if (buffer.Length > maxBytes)
        {
            // The declared length is the client's claim; the bytes actually read are what counts.
            return Invalid("file", $"Image must be between 1 byte and {maxBytes} bytes.");
        }
        // Trust the bytes, not the file name or the client's content type. Anything else (SVG, GIF, HTML, ...) is refused;
        // the stored name is a server-generated GUID with the extension of the detected format.
        var extension = DetectImageExtension(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
        if (extension is null)
        {
            return Invalid("file", "Only JPEG, PNG and WebP images are accepted.");
        }

        if (!string.IsNullOrWhiteSpace(upload.Alt) && PersianText.Normalize(upload.Alt).Length > ProductImage.AltMaxLength)
        {
            return Invalid("alt", $"At most {ProductImage.AltMaxLength} characters.");
        }

        buffer.Position = 0;
        var path = await media.SaveAsync(buffer, $"products/{productId}", extension, ct);
        var sortOrder = product.Images.Count == 0 ? 0 : product.Images.Max(i => i.SortOrder) + 1;
        var image = ProductImage.Create(path, string.IsNullOrWhiteSpace(upload.Alt) ? product.Name : upload.Alt, sortOrder);
        product.Images.Add(image);
        product.Touch(clock.GetUtcNow().UtcDateTime);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await media.DeleteAsync(path, CancellationToken.None);
            throw;
        }

        return ToDto(image);
    }

    public async Task<Result<AdminProductDto>> ReorderImagesAsync(int productId, ImageOrderRequest request, CancellationToken ct)
    {
        var product = await LoadProductAsync(productId, tracking: true, ct);
        if (product is null)
        {
            return NotFound("product");
        }

        if (!product.Images.Select(i => i.Id).ToHashSet().SetEquals(request.ImageIds) || product.Images.Count != request.ImageIds.Count)
        {
            return Invalid("imageIds", "Must list every image of the product exactly once.");
        }

        for (var i = 0; i < request.ImageIds.Count; i++)
        {
            product.Images.Single(image => image.Id == request.ImageIds[i]).MoveTo(i);
        }

        product.Touch(clock.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
        return ToDto(product);
    }

    public async Task<Result> DeleteImageAsync(int imageId, CancellationToken ct)
    {
        var image = await db.ProductImages.SingleOrDefaultAsync(i => i.Id == imageId, ct);
        if (image is null)
        {
            return Result.Failure(NotFound("image"));
        }

        db.ProductImages.Remove(image);
        await db.Products.Where(p => p.Id == image.ProductId).ForEachAsync(p => p.Touch(clock.GetUtcNow().UtcDateTime), ct);
        await db.SaveChangesAsync(ct);
        await media.DeleteAsync(image.Path, ct);
        return Result.Success();
    }

    internal static string? DetectImageExtension(ReadOnlySpan<byte> bytes) => bytes switch
    {
        [0xFF, 0xD8, 0xFF, ..] => ".jpg",
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, ..] => ".png",
        [(byte)'R', (byte)'I', (byte)'F', (byte)'F', _, _, _, _, (byte)'W', (byte)'E', (byte)'B', (byte)'P', ..] => ".webp",
        _ => null,
    };

    // ---- helpers ----

    private async Task<(Error? Error, string? BrandName)> ValidateReferencesAsync(ProductRequest request, CancellationToken ct)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == request.CategoryId, ct))
        {
            return (Invalid("categoryId", "Category does not exist."), null);
        }

        if (request.BrandId is null)
        {
            return (null, null);
        }

        var brandName = await db.Brands.Where(b => b.Id == request.BrandId).Select(b => b.Name).SingleOrDefaultAsync(ct);
        return brandName is null ? (Invalid("brandId", "Brand does not exist."), null) : (null, brandName);
    }

    private Task<Product?> LoadProductAsync(int id, bool tracking, CancellationToken ct)
    {
        var query = db.Products.Include(p => p.Variants).Include(p => p.Images).Include(p => p.AttributeValues).AsSplitQuery();
        return (tracking ? query : query.AsNoTracking()).SingleOrDefaultAsync(p => p.Id == id, ct);
    }

    private Task<Result<T>> SaveAsync<T>(Func<T> onSuccess, (string Index, Error Error) unique, CancellationToken ct) =>
        SaveAsync(onSuccess, [unique], ct);

    private async Task<Result<T>> SaveAsync<T>(Func<T> onSuccess, (string Index, Error Error)[] uniques, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return onSuccess();
        }
        catch (DbUpdateException ex) when (uniques.Any(u => DbErrors.IsUniqueViolation(ex, u.Index)))
        {
            // Lost a race with a concurrent request: the unique index decides.
            return uniques.First(u => DbErrors.IsUniqueViolation(ex, u.Index)).Error;
        }
    }

    private static Error Invalid(string field, string message) =>
        Error.Validation(new Dictionary<string, string[]> { [field] = [message] });

    private static Error NotFound(string what) => Error.NotFound($"{what}_not_found", $"The {what} was not found.");

    private static AdminCategoryDto ToDto(Category c) => new(c.Id, c.Slug, c.Name, c.ParentId, c.Description, c.SortOrder, c.IsActive);

    private static AdminBrandDto ToDto(Brand b) => new(b.Id, b.Slug, b.Name, b.IsActive);

    private static AdminAttributeOptionDto ToDto(AttributeOption o) => new(o.Id, o.Value, o.Slug, o.SortOrder);

    private static AdminVariantDto ToDto(ProductVariant v) =>
        new(v.Id, v.Sku, v.Price, v.CompareAtPrice, v.Stock, v.IsActive, v.SortOrder, v.RowVersion);

    private AdminImageDto ToDto(ProductImage i) => new(i.Id, media.GetUrl(i.Path), i.Alt, i.SortOrder);

    private AdminProductDto ToDto(Product p) => new(
        p.Id, p.Slug, p.Name, p.Description, p.CategoryId, p.BrandId, p.Status, p.CreatedAtUtc, p.UpdatedAtUtc,
        p.Variants.OrderBy(v => v.SortOrder).ThenBy(v => v.Id).Select(ToDto).ToList(),
        p.Images.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).Select(ToDto).ToList(),
        p.AttributeValues.Select(v => new AdminAttributeValueDto(v.AttributeId, v.VariantId, v.OptionId, v.Text)).ToList());
}
