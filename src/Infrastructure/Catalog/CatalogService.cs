using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Shop.Application.Catalog;
using Shop.Application.Common;
using Shop.Domain.Catalog;
using Shop.Domain.Common;
using Shop.Infrastructure.Persistence;

namespace Shop.Infrastructure.Catalog;

internal sealed class CatalogService(ShopDbContext db, IMediaStorage media) : ICatalogService
{
    private static readonly Error CategoryNotFound = Error.NotFound("category_not_found", "Category was not found.");
    private static readonly Error ProductNotFound = Error.NotFound("product_not_found", "Product was not found.");

    /// <summary>Displayed price of a product: its cheapest active variant.</summary>
    private static readonly Expression<Func<Product, long?>> MinPrice =
        p => p.Variants.Where(v => v.IsActive).Min(v => (long?)v.Price);

    public async Task<IReadOnlyList<CategoryTreeDto>> GetCategoryTreeAsync(CancellationToken ct)
    {
        var visible = await VisibleCategoriesAsync(ct);
        return visible.Where(c => c.ParentId is null)
            .Select(root => new CategoryTreeDto(root.Slug, root.Name, ChildrenOf(visible, root.Id)))
            .ToList();
    }

    public async Task<Result<CategoryDetailDto>> GetCategoryAsync(string slug, CancellationToken ct)
    {
        var visible = await VisibleCategoriesAsync(ct);
        var category = visible.SingleOrDefault(c => c.Slug == slug);
        if (category is null)
        {
            return CategoryNotFound;
        }

        var parent = visible.SingleOrDefault(c => c.Id == category.ParentId);
        return new CategoryDetailDto(
            category.Slug,
            category.Name,
            category.Description,
            parent is null ? null : new CategoryLinkDto(parent.Slug, parent.Name),
            ChildrenOf(visible, category.Id));
    }

    public async Task<IReadOnlyList<BrandDto>> GetBrandsAsync(CancellationToken ct) =>
        await db.Brands.AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.Name)
            .Select(b => new BrandDto(b.Slug, b.Name))
            .ToListAsync(ct);

    public async Task<Result<ProductListDto>> GetProductsAsync(ProductListQuery query, CancellationToken ct)
    {
        List<int>? categoryIds = null;
        if (query.Category is not null)
        {
            var visible = await VisibleCategoriesAsync(ct);
            var category = visible.SingleOrDefault(c => c.Slug == query.Category);
            if (category is null)
            {
                return CategoryNotFound;
            }

            // A root category also lists its subcategories' products.
            categoryIds = [category.Id, .. visible.Where(c => c.ParentId == category.Id).Select(c => c.Id)];
        }

        var brands = await db.Brands.AsNoTracking().Where(b => b.IsActive).Select(b => new { b.Id, b.Slug, b.Name }).ToListAsync(ct);
        var attributes = await db.Attributes.AsNoTracking()
            .Where(a => a.IsFilterable && a.Type == AttributeType.Option)
            .Include(a => a.Options)
            .OrderBy(a => a.SortOrder)
            .ToListAsync(ct);

        var filter = new ListFilter
        {
            CategoryIds = categoryIds,
            Tokens = PersianText.SearchKey(query.Q).Split(' ', StringSplitOptions.RemoveEmptyEntries),
            InStock = query.InStock,
            MinPrice = query.MinPrice,
            MaxPrice = query.MaxPrice,
            // Unknown slugs simply match nothing: brand=unknown gives an empty list, like any filter with no matches.
            BrandIds = query.Brands is { Count: > 0 } wanted
                ? brands.Where(b => wanted.Contains(b.Slug)).Select(b => b.Id).ToList()
                : null,
            // Unknown attribute codes are ignored (they may be tracking parameters); unknown option slugs match nothing.
            Attributes = (query.Attributes ?? new Dictionary<string, IReadOnlyList<string>>())
                .Select(pair => (Attribute: attributes.SingleOrDefault(a => a.Code == pair.Key), Slugs: pair.Value))
                .Where(x => x.Attribute is not null && x.Slugs.Count > 0)
                .ToDictionary(
                    x => x.Attribute!.Id,
                    x => x.Attribute!.Options.Where(o => x.Slugs.Contains(o.Slug)).Select(o => (int?)o.Id).ToList()),
        };

        var matching = Apply(filter, except: null);
        var total = await matching.CountAsync(ct);
        var ordered = query.Sort switch
        {
            ProductSort.PriceAsc => matching.OrderBy(MinPrice).ThenBy(p => p.Id),
            ProductSort.PriceDesc => matching.OrderByDescending(MinPrice).ThenBy(p => p.Id),
            _ => matching.OrderByDescending(p => p.CreatedAtUtc).ThenByDescending(p => p.Id),
        };

        var rows = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(p => new
            {
                p.Slug,
                p.Name,
                Brand = db.Brands.Where(b => b.Id == p.BrandId).Select(b => b.Name).FirstOrDefault(),
                Cheapest = p.Variants.Where(v => v.IsActive).OrderBy(v => v.Price).ThenBy(v => v.Id)
                    .Select(v => new { v.Price, v.CompareAtPrice }).First(),
                InStock = p.Variants.Any(v => v.IsActive && v.Stock > 0),
                Image = p.Images.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).Select(i => new { i.Path, i.Alt }).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new ProductCardDto(
                r.Slug, r.Name, r.Brand, r.Cheapest.Price, r.Cheapest.CompareAtPrice, r.InStock,
                r.Image is null ? null : new ImageDto(media.GetUrl(r.Image.Path), r.Image.Alt)))
            .ToList();

        var brandCounts = await Apply(filter, except: Facet.Brand)
            .Where(p => p.BrandId != null)
            .GroupBy(p => p.BrandId!.Value)
            .Select(g => new { BrandId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BrandId, x => x.Count, ct);
        var brandFacet = brands.Where(b => brandCounts.ContainsKey(b.Id))
            .OrderBy(b => b.Name)
            .Select(b => new FacetOptionDto(b.Slug, b.Name, brandCounts[b.Id]))
            .ToList();

        var attributeFacets = await AttributeFacetsAsync(filter, attributes, ct);
        var price = await PriceRangeAsync(filter, ct);

        return new ProductListDto(items, query.Page, query.PageSize, total, new ProductFacetsDto(brandFacet, attributeFacets, price));
    }

    public async Task<Result<ProductDetailDto>> GetProductAsync(string slug, CancellationToken ct)
    {
        var product = await db.Products.AsNoTracking()
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .Include(p => p.AttributeValues)
            .AsSplitQuery()
            .SingleOrDefaultAsync(p => p.Slug == slug && p.Status != ProductStatus.Draft, ct);
        if (product is null)
        {
            return ProductNotFound;
        }

        var brand = product.BrandId is null
            ? null
            : await db.Brands.AsNoTracking().Where(b => b.Id == product.BrandId).Select(b => new BrandDto(b.Slug, b.Name)).SingleAsync(ct);

        var breadcrumb = new List<CategoryLinkDto>();
        var category = await db.Categories.AsNoTracking().SingleAsync(c => c.Id == product.CategoryId, ct);
        if (category.ParentId is { } parentId)
        {
            var parent = await db.Categories.AsNoTracking().SingleAsync(c => c.Id == parentId, ct);
            breadcrumb.Add(new CategoryLinkDto(parent.Slug, parent.Name));
        }

        breadcrumb.Add(new CategoryLinkDto(category.Slug, category.Name));

        var attributeIds = product.AttributeValues.Select(v => v.AttributeId).Distinct().ToList();
        var attributes = await db.Attributes.AsNoTracking()
            .Where(a => attributeIds.Contains(a.Id))
            .Include(a => a.Options)
            .ToDictionaryAsync(a => a.Id, ct);
        string Display(ProductAttributeValue v) => v.Text ?? attributes[v.AttributeId].Options.Single(o => o.Id == v.OptionId).Value;
        string Key(ProductAttributeValue v) => v.Text ?? attributes[v.AttributeId].Options.Single(o => o.Id == v.OptionId).Slug;

        var purchasable = product.Status == ProductStatus.Published;
        var variants = product.Variants.Where(v => v.IsActive).OrderBy(v => v.SortOrder).ThenBy(v => v.Id).ToList();
        var variantIds = variants.Select(v => v.Id).ToHashSet();
        var variantValues = product.AttributeValues.Where(v => v.VariantId is { } id && variantIds.Contains(id)).ToList();

        var variantDtos = variants.Select(v => new VariantDto(
                v.Id, v.Sku, v.Price, v.CompareAtPrice, purchasable && v.Stock > 0,
                variantValues.Where(x => x.VariantId == v.Id)
                    .ToDictionary(x => attributes[x.AttributeId].Code, Key)))
            .ToList();

        // Selectors: option attributes that vary per variant, with only the options some active variant has.
        var axes = variantValues.Where(v => v.OptionId is not null)
            .GroupBy(v => v.AttributeId)
            .Select(g => attributes[g.Key])
            .OrderBy(a => a.SortOrder)
            .Select(a => new VariantAxisDto(a.Code, a.Name, a.Unit, a.Options
                .Where(o => variantValues.Any(v => v.OptionId == o.Id))
                .OrderBy(o => o.SortOrder)
                .Select(o => new AxisOptionDto(o.Slug, o.Value))
                .ToList()))
            .ToList();

        var specs = product.AttributeValues.Where(v => v.VariantId is null)
            .OrderBy(v => attributes[v.AttributeId].SortOrder)
            .Select(v => new SpecDto(attributes[v.AttributeId].Code, attributes[v.AttributeId].Name, Display(v), attributes[v.AttributeId].Unit))
            .ToList();

        var images = product.Images.OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
            .Select(i => new ImageDto(media.GetUrl(i.Path), i.Alt))
            .ToList();

        return new ProductDetailDto(
            product.Slug, product.Name, product.Description, product.Status.ToString().ToLowerInvariant(),
            brand, breadcrumb, images, axes, variantDtos, specs, product.UpdatedAtUtc);
    }

    public async Task<CatalogSitemapDto> GetSitemapAsync(CancellationToken ct)
    {
        var categories = (await VisibleCategoriesAsync(ct)).Select(c => new SitemapEntryDto(c.Slug, null)).ToList();
        var products = await db.Products.AsNoTracking()
            .Where(p => p.Status == ProductStatus.Published && p.Variants.Any(v => v.IsActive))
            .OrderBy(p => p.Id)
            .Select(p => new SitemapEntryDto(p.Slug, p.UpdatedAtUtc))
            .ToListAsync(ct);
        return new CatalogSitemapDto(categories, products);
    }

    // ---- listing internals ----

    private enum Facet
    {
        Brand,
        Price,
        Attribute,
    }

    private sealed class ListFilter
    {
        public List<int>? CategoryIds { get; init; }
        public string[] Tokens { get; init; } = [];
        public bool InStock { get; init; }
        public long? MinPrice { get; init; }
        public long? MaxPrice { get; init; }
        public List<int>? BrandIds { get; init; }
        public Dictionary<int, List<int?>> Attributes { get; init; } = [];
    }

    /// <summary>
    /// Listed products matching the filter. <paramref name="except"/> leaves one facet's own selection out, so its
    /// counts show what each option would give (OR within a facet, AND across facets).
    /// </summary>
    private IQueryable<Product> Apply(ListFilter filter, Facet? except, int? exceptAttributeId = null)
    {
        var query = db.Products.AsNoTracking()
            .Where(p => p.Status == ProductStatus.Published && p.Variants.Any(v => v.IsActive));

        if (filter.CategoryIds is { } categoryIds)
        {
            query = query.Where(p => categoryIds.Contains(p.CategoryId));
        }

        foreach (var token in filter.Tokens)
        {
            query = query.Where(p => p.SearchKey.Contains(token));
        }

        if (filter.InStock)
        {
            query = query.Where(p => p.Variants.Any(v => v.IsActive && v.Stock > 0));
        }

        if (except != Facet.Brand && filter.BrandIds is { } brandIds)
        {
            query = query.Where(p => p.BrandId != null && brandIds.Contains(p.BrandId.Value));
        }

        if (except != Facet.Price && filter.MinPrice is { } min)
        {
            query = query.Where(p => p.Variants.Where(v => v.IsActive).Min(v => (long?)v.Price) >= min);
        }

        if (except != Facet.Price && filter.MaxPrice is { } max)
        {
            query = query.Where(p => p.Variants.Where(v => v.IsActive).Min(v => (long?)v.Price) <= max);
        }

        foreach (var (attributeId, optionIds) in filter.Attributes)
        {
            if (except == Facet.Attribute && attributeId == exceptAttributeId)
            {
                continue;
            }

            // Matches when the product (spec) or one of its active variants has one of the options.
            query = query.Where(p => p.AttributeValues.Any(v =>
                v.AttributeId == attributeId && optionIds.Contains(v.OptionId) && (v.VariantId == null || v.Variant!.IsActive)));
        }

        return query;
    }

    private async Task<List<AttributeFacetDto>> AttributeFacetsAsync(
        ListFilter filter, List<AttributeDefinition> attributes, CancellationToken ct)
    {
        // Attributes without a selection share one query over the fully filtered set; each selected attribute needs
        // its own query with its selection left out.
        var counts = new Dictionary<int, int>();
        var unselected = attributes.Select(a => a.Id).Where(id => !filter.Attributes.ContainsKey(id)).ToList();
        if (unselected.Count > 0)
        {
            await CountOptionsAsync(Apply(filter, except: null), unselected, counts, ct);
        }

        foreach (var attributeId in filter.Attributes.Keys)
        {
            await CountOptionsAsync(Apply(filter, Facet.Attribute, attributeId), [attributeId], counts, ct);
        }

        return attributes
            .Select(a => new AttributeFacetDto(a.Code, a.Name, a.Unit, a.Options
                .Where(o => counts.ContainsKey(o.Id))
                .OrderBy(o => o.SortOrder)
                .Select(o => new FacetOptionDto(o.Slug, o.Value, counts[o.Id]))
                .ToList()))
            .Where(f => f.Options.Count > 0)
            .ToList();
    }

    private async Task CountOptionsAsync(IQueryable<Product> products, List<int> attributeIds, Dictionary<int, int> counts, CancellationToken ct)
    {
        var productIds = products.Select(p => p.Id);
        var rows = await db.ProductAttributeValues.AsNoTracking()
            .Where(v => v.OptionId != null && attributeIds.Contains(v.AttributeId)
                && (v.VariantId == null || v.Variant!.IsActive)
                && productIds.Contains(v.ProductId))
            .GroupBy(v => v.OptionId!.Value)
            .Select(g => new { OptionId = g.Key, Count = g.Select(v => v.ProductId).Distinct().Count() })
            .ToListAsync(ct);
        foreach (var row in rows)
        {
            counts[row.OptionId] = row.Count;
        }
    }

    private async Task<PriceRangeDto?> PriceRangeAsync(ListFilter filter, CancellationToken ct)
    {
        // Two ordered lookups instead of MIN/MAX: SQL Server can't aggregate over the per-product MIN subquery.
        var products = Apply(filter, Facet.Price);
        var min = await products.OrderBy(MinPrice).Select(MinPrice).FirstOrDefaultAsync(ct);
        var max = await products.OrderByDescending(MinPrice).Select(MinPrice).FirstOrDefaultAsync(ct);
        return min is null || max is null ? null : new PriceRangeDto(min.Value, max.Value);
    }

    private async Task<List<Category>> VisibleCategoriesAsync(CancellationToken ct)
    {
        // Small table: load it and resolve the two-level tree in memory. A child of an inactive root is hidden too.
        var active = await db.Categories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .ToListAsync(ct);
        var activeIds = active.Select(c => c.Id).ToHashSet();
        return active.Where(c => c.ParentId is null || activeIds.Contains(c.ParentId.Value)).ToList();
    }

    private static List<CategoryLinkDto> ChildrenOf(List<Category> categories, int parentId) =>
        categories.Where(c => c.ParentId == parentId).Select(c => new CategoryLinkDto(c.Slug, c.Name)).ToList();
}
