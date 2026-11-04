using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Shop.Application.Catalog.Transfer;
using Shop.Application.Common;
using Shop.Application.Routing;
using Shop.Application.Translations;
using Shop.Domain.Catalog;
using Shop.Domain.Common;
using Shop.Domain.Inventory;
using Shop.Domain.Routing;
using Shop.Infrastructure.Persistence;

namespace Shop.Infrastructure.Catalog;

/// <summary>
/// Products as a table (docs/16). One row per variant. Export writes every product; import reads the same columns. The dry run
/// (<see cref="PreviewAsync"/>) and the real run (<see cref="ApplyAsync"/>) use the same planner, so what the preview promises is
/// what the apply does, and the apply is one transaction: a single bad row writes nothing. Images are never fetched from a
/// URL (no outgoing calls): the only image column is the ids of the product's OWN images, used to put them in a new order.
/// </summary>
internal sealed class CatalogTransferService(ShopDbContext db, IRouteService routes, ICurrentUser currentUser, TimeProvider clock) : ICatalogTransferService
{
    public const int MaxReportedErrors = 200;
    public const int MaxExportRows = 20_000;

    // Fixed columns, in file order. Attribute columns follow: spec:<code> (a value of the whole product), axis:<code> (per variant).
    private static readonly string[] Fixed =
    [
        "product_slug", "product_name", "category", "brand", "status", "description", "path",
        "sku", "price", "compare_at_price", "stock", "variant_active", "sort_order", "image_ids",
    ];

    private static readonly HashSet<string> NumericColumns = ["price", "compare_at_price", "stock", "sort_order"];
    private const string SpecPrefix = "spec:";
    private const string AxisPrefix = "axis:";

    private static string T(string key, params (string, object?)[] args) => Translate.Resolve(Msg.Of(key, args));

    // ---- export ---------------------------------------------------------------------------------------------------------

    public async Task<ExportFile> ExportAsync(string format, CancellationToken ct)
    {
        format = format == "xlsx" ? "xlsx" : "csv";
        var attributes = await db.Attributes.AsNoTracking().Include(a => a.Options).OrderBy(a => a.SortOrder).ThenBy(a => a.Code).ToListAsync(ct);
        var categories = await db.Categories.AsNoTracking().ToDictionaryAsync(c => c.Id, ct);
        var brands = await db.Brands.AsNoTracking().ToDictionaryAsync(b => b.Id, b => b.Name, ct);
        var categoryPaths = await CurrentPathsAsync(RouteTargetTypes.Category, ct);
        var productPaths = await CurrentPathsAsync(RouteTargetTypes.Product, ct);
        var products = await db.Products.AsNoTracking().Include(p => p.Variants).Include(p => p.AttributeValues).Include(p => p.Images)
            .AsSplitQuery().OrderBy(p => p.Slug).ToListAsync(ct);

        var header = Fixed.Concat(attributes.Select(a => SpecPrefix + a.Code)).Concat(attributes.Select(a => AxisPrefix + a.Code)).ToList();
        var numeric = header.Select((name, i) => (name, i)).Where(x => NumericColumns.Contains(x.name)).Select(x => x.i).ToHashSet();
        var rows = new List<IReadOnlyList<string>>();
        foreach (var product in products)
        {
            var optionText = attributes.SelectMany(a => a.Options.Select(o => (o.Id, o.Value))).ToDictionary(x => x.Id, x => x.Value);
            string ValueOf(ProductAttributeValue v) => v.OptionId is { } id ? optionText.GetValueOrDefault(id, string.Empty) : v.Text ?? string.Empty;
            var images = string.Join(',', product.Images.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).Select(i => i.Id));
            foreach (var variant in product.Variants.OrderBy(v => v.SortOrder).ThenBy(v => v.Id))
            {
                var row = new List<string>
                {
                    product.Slug, product.Name, categoryPaths.GetValueOrDefault(product.CategoryId, categories.GetValueOrDefault(product.CategoryId)?.Slug ?? string.Empty),
                    product.BrandId is { } b ? brands.GetValueOrDefault(b, string.Empty) : string.Empty,
                    product.Status.ToString().ToLowerInvariant(), product.Description ?? string.Empty, productPaths.GetValueOrDefault(product.Id, string.Empty),
                    variant.Sku, variant.Price.ToString(CultureInfo.InvariantCulture), variant.CompareAtPrice?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    variant.Stock.ToString(CultureInfo.InvariantCulture), variant.IsActive ? "1" : "0", variant.SortOrder.ToString(CultureInfo.InvariantCulture), images,
                };
                row.AddRange(attributes.Select(a => product.AttributeValues.FirstOrDefault(v => v.AttributeId == a.Id && v.VariantId is null) is { } spec ? ValueOf(spec) : string.Empty));
                row.AddRange(attributes.Select(a => product.AttributeValues.FirstOrDefault(v => v.AttributeId == a.Id && v.VariantId == variant.Id) is { } axis ? ValueOf(axis) : string.Empty));
                rows.Add(row.Select(Safe).ToList());
                if (rows.Count >= MaxExportRows)
                {
                    break;
                }
            }

            if (rows.Count >= MaxExportRows)
            {
                break;
            }
        }

        var bytes = TableCodec.Write(format, header, rows, numeric);
        var now = clock.GetUtcNow().UtcDateTime;
        var name = $"products-{now:yyyyMMdd-HHmm}.{format}";
        db.CatalogTransferLogs.Add(CatalogTransferLog.Create(currentUser.UserId, CatalogTransferLog.ExportKind, name, rows.Count, 0, 0, 0, 0, now));
        await db.SaveChangesAsync(ct);
        return new ExportFile(name, format == "xlsx" ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" : "text/csv; charset=utf-8", bytes);
    }

    /// <summary>A cell that a spreadsheet program could take for a formula gets a leading apostrophe (the import takes it off again).</summary>
    private static string Safe(string value) => value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + value : value;

    private static string Unsafe(string value) => value.Length > 1 && value[0] == '\'' && value[1] is '=' or '+' or '-' or '@' or '\t' or '\r' ? value[1..] : value;

    private async Task<Dictionary<int, string>> CurrentPathsAsync(string type, CancellationToken ct) =>
        await db.RoutePaths.AsNoTracking().Where(r => r.TargetType == type && r.IsCurrent && !r.IsGone).ToDictionaryAsync(r => r.TargetId, r => r.Path, ct);

    // ---- import: dry run and apply -----------------------------------------------------------------------------------------

    public async Task<Result<ImportPreviewDto>> PreviewAsync(Stream file, string fileName, CancellationToken ct)
    {
        var (table, problem) = ReadFile(file, fileName);
        if (table is null)
        {
            return FileProblem(problem!);
        }

        var plan = await PlanAsync(table, tracking: false, ct);
        return plan.ToPreview();
    }

    public async Task<Result<ImportAppliedDto>> ApplyAsync(Stream file, string fileName, CancellationToken ct)
    {
        var (table, problem) = ReadFile(file, fileName);
        if (table is null)
        {
            return FileProblem(problem!);
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var plan = await PlanAsync(table, tracking: true, ct);
        if (plan.Errors.Count > 0)
        {
            return Error.Validation(new Dictionary<string, string[]> { ["file"] = ["api.import.has-errors"] });
        }

        try
        {
            await ApplyPlanAsync(plan, ct);
            var s = plan.Summary();
            db.CatalogTransferLogs.Add(CatalogTransferLog.Create(
                currentUser.UserId, CatalogTransferLog.ImportKind, fileName ?? string.Empty, s.Rows, s.ProductsToCreate, s.ProductsToUpdate, s.VariantsToCreate, s.VariantsToUpdate, clock.GetUtcNow().UtcDateTime));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Error.Conflict("concurrent_update", "api.import.concurrent-update");
        }
        catch (DbUpdateException)
        {
            return Error.Conflict("import_conflict", "api.import.conflict");
        }

        await routes.CompleteAsync(ct);
        return new ImportAppliedDto(plan.Summary());
    }

    private static (TableFile? Table, string? Problem) ReadFile(Stream file, string fileName)
    {
        try
        {
            return (TableCodec.Read(file, fileName), null);
        }
        catch (TableFileException ex)
        {
            return (null, ex.Message);
        }
    }

    private static Error FileProblem(string message) => Error.Validation(new Dictionary<string, string[]> { ["file"] = [message] });

    // ---- the plan -----------------------------------------------------------------------------------------------------------

    private sealed record Attr(int Id, string Code, AttributeType Type, List<AttributeOption> Options);

    private sealed record AttrValue(int? OptionId, string? Text);

    private sealed class PlannedVariant(int row, string sku)
    {
        public int Row { get; } = row;
        public string Sku { get; } = sku;
        public long Price { get; set; }
        public long? CompareAt { get; set; }
        public int? Stock { get; set; }
        public bool? Active { get; set; }
        public int? Sort { get; set; }
        public ProductVariant? Existing { get; set; }
        public Dictionary<int, AttrValue?> Axis { get; } = [];
        public string Action { get; set; } = "create-variant";
    }

    private sealed class PlannedProduct(string slug, int firstRow)
    {
        public string Slug { get; } = slug;
        public int FirstRow { get; } = firstRow;
        public Product? Existing { get; set; }
        public string? Name { get; set; }
        public int? CategoryId { get; set; }
        public bool CategoryGiven { get; set; }
        public int? BrandId { get; set; }
        public string? BrandName { get; set; }
        public ProductStatus? Status { get; set; }
        public string? Description { get; set; }
        public string? Path { get; set; }
        public List<PlannedVariant> Variants { get; } = [];
        public Dictionary<int, AttrValue?> Spec { get; } = [];
        public HashSet<int> SpecColumns { get; } = [];
        public HashSet<int> AxisColumns { get; } = [];
        public List<int>? ImageOrder { get; set; }
        public bool ProductChanged { get; set; }
        public bool IsNew => Existing is null;
    }

    private sealed class Plan
    {
        public List<PlannedProduct> Products { get; } = [];
        public List<ImportRowError> Errors { get; } = [];
        public int RowCount { get; set; }
        public int ErrorCount { get; private set; }

        public void Error(int row, string column, string message)
        {
            ErrorCount++;
            if (Errors.Count < MaxReportedErrors)
            {
                Errors.Add(new ImportRowError(row, column, message));
            }
        }

        public ImportSummary Summary() => new(
            RowCount,
            Products.Count(p => p.IsNew),
            Products.Count(p => !p.IsNew && p.ProductChanged),
            Products.Sum(p => p.Variants.Count(v => v.Action is "create-variant" or "create-product")),
            Products.Sum(p => p.Variants.Count(v => v.Action == "update-variant")),
            Products.Sum(p => p.Variants.Count(v => v.Action == "unchanged")));

        public Result<ImportPreviewDto> ToPreview() => new ImportPreviewDto(
            ErrorCount == 0, Summary(),
            Products.SelectMany(p => p.Variants.Select((v, i) => new ImportRowPreview(v.Row, p.Slug, v.Sku, p.IsNew && i == 0 ? "create-product" : v.Action))).OrderBy(r => r.Row).ToList(),
            Errors.OrderBy(e => e.Row).ToList(), ErrorCount > Errors.Count);
    }

    private async Task<Plan> PlanAsync(TableFile table, bool tracking, CancellationToken ct)
    {
        var plan = new Plan { RowCount = table.Rows.Count };

        // ---- header ----
        var attributes = (await db.Attributes.AsNoTracking().Include(a => a.Options).ToListAsync(ct))
            .Select(a => new Attr(a.Id, a.Code, a.Type, a.Options.ToList())).ToDictionary(a => a.Code, StringComparer.Ordinal);
        var columns = new Dictionary<string, int>(StringComparer.Ordinal);
        var specColumns = new Dictionary<int, Attr>();
        var axisColumns = new Dictionary<int, Attr>();
        for (var i = 0; i < table.Header.Count; i++)
        {
            var name = table.Header[i].Trim().ToLowerInvariant();
            if (name.Length == 0)
            {
                continue;
            }

            if (columns.ContainsKey(name))
            {
                plan.Error(0, name, T("api.import.header-duplicate", ("column", name)));
            }
            else if (Fixed.Contains(name))
            {
                columns[name] = i;
            }
            else if ((name.StartsWith(SpecPrefix, StringComparison.Ordinal) || name.StartsWith(AxisPrefix, StringComparison.Ordinal)) && attributes.TryGetValue(name[(name.IndexOf(':') + 1)..], out var attr))
            {
                columns[name] = i;
                (name.StartsWith(SpecPrefix, StringComparison.Ordinal) ? specColumns : axisColumns)[i] = attr;
            }
            else
            {
                plan.Error(0, name, T("api.import.header-unknown", ("column", name)));
            }
        }

        foreach (var required in new[] { "product_slug", "product_name", "sku", "price" })
        {
            if (!columns.ContainsKey(required))
            {
                plan.Error(0, required, T("api.import.header-missing", ("column", required)));
            }
        }

        if (plan.ErrorCount > 0)
        {
            return plan;
        }

        string Cell(TableRow row, string column) => columns.TryGetValue(column, out var i) && i < row.Cells.Count ? Unsafe(row.Cells[i].Trim()) : string.Empty;
        bool Has(string column) => columns.ContainsKey(column);

        // ---- reference data ----
        var categoryIds = await db.Categories.AsNoTracking().ToDictionaryAsync(c => c.Slug, c => c.Id, StringComparer.OrdinalIgnoreCase, ct);
        var categoryPaths = (await CurrentPathsAsync(RouteTargetTypes.Category, ct)).ToDictionary(x => x.Value, x => x.Key, StringComparer.OrdinalIgnoreCase);
        var brands = (await db.Brands.AsNoTracking().ToListAsync(ct)).GroupBy(b => PersianText.SearchKey(b.Name)).ToDictionary(g => g.Key, g => g.First());

        var slugs = table.Rows.Select(r => Cell(r, "product_slug")).Where(s => s.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        var skus = table.Rows.Select(r => Cell(r, "sku")).Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        IQueryable<Product> productQuery = db.Products;
        productQuery = tracking ? productQuery : productQuery.AsNoTracking();
        var existing = (await productQuery.Include(p => p.Variants).Include(p => p.AttributeValues).Include(p => p.Images).AsSplitQuery()
            .Where(p => slugs.Contains(p.Slug)).ToListAsync(ct)).ToDictionary(p => p.Slug, StringComparer.Ordinal);
        var elsewhere = await db.ProductVariants.AsNoTracking().Where(v => skus.Contains(v.Sku))
            .Join(db.Products.AsNoTracking(), v => v.ProductId, p => p.Id, (v, p) => new { v.Sku, p.Slug }).ToListAsync(ct);
        var skuOwner = elsewhere.ToDictionary(x => x.Sku, x => x.Slug, StringComparer.OrdinalIgnoreCase);
        var productPaths = await CurrentPathsAsync(RouteTargetTypes.Product, ct);

        // ---- rows ----
        var byProduct = new Dictionary<string, PlannedProduct>(StringComparer.Ordinal);
        var seenSkus = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var newPaths = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in table.Rows)
        {
            var slug = Cell(row, "product_slug");
            var sku = Cell(row, "sku");
            if (!Slug.IsValid(slug))
            {
                plan.Error(row.Number, "product_slug", T("api.import.e.slug-invalid", ("max", Slug.MaxLength)));
                continue;
            }

            if (!ProductVariant.IsValidSku(sku))
            {
                plan.Error(row.Number, "sku", T("api.import.e.sku-invalid", ("max", ProductVariant.SkuMaxLength)));
                continue;
            }

            if (seenSkus.TryGetValue(sku, out var firstRow))
            {
                plan.Error(row.Number, "sku", T("api.import.e.sku-duplicate", ("row", firstRow)));
                continue;
            }

            seenSkus[sku] = row.Number;

            if (!byProduct.TryGetValue(slug, out var product))
            {
                product = new PlannedProduct(slug, row.Number) { Existing = existing.GetValueOrDefault(slug) };
                byProduct[slug] = product;
                plan.Products.Add(product);
            }

            ReadProductFields(plan, row, product, Cell, categoryIds, categoryPaths, brands, attributes, specColumns);
            ReadVariant(plan, row, product, sku, Cell, Has, skuOwner, attributes, axisColumns);
        }

        // ---- per product checks that need the whole group ----
        foreach (var product in plan.Products)
        {
            if (product.IsNew)
            {
                if (string.IsNullOrEmpty(product.Name))
                {
                    plan.Error(product.FirstRow, "product_name", T("api.import.e.name-required"));
                }

                if (!product.CategoryGiven)
                {
                    plan.Error(product.FirstRow, "category", T("api.import.e.category-required"));
                }

                var pathCheck = await routes.CheckAvailableForNewAsync(RouteTargetTypes.Product, product.Slug, product.Path, ct);
                if (!pathCheck.IsSuccess)
                {
                    plan.Error(product.FirstRow, "path", T("api.import.e.path-unavailable", ("detail", Translate.Resolve(pathCheck.Error!.MessageKey))));
                }
                else
                {
                    var wanted = string.IsNullOrWhiteSpace(product.Path) ? RouteTargetTypes.DefaultPath(RouteTargetTypes.Product, product.Slug) : RoutePath.Normalize(product.Path);
                    if (newPaths.TryGetValue(wanted, out var other))
                    {
                        plan.Error(product.FirstRow, "path", T("api.import.e.path-duplicate", ("row", other)));
                    }
                    else
                    {
                        newPaths[wanted] = product.FirstRow;
                    }
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(product.Path) && RoutePath.Normalize(product.Path) is var wantedPath && wantedPath != productPaths.GetValueOrDefault(product.Existing!.Id))
                {
                    plan.Error(product.FirstRow, "path", T("api.import.e.path-readonly", ("current", productPaths.GetValueOrDefault(product.Existing.Id, string.Empty))));
                }
            }

            ValidateAttributeRules(plan, product, attributes.Values.ToDictionary(a => a.Id, a => a.Code));
            ValidateImages(plan, product);
            ClassifyVariants(product);
        }

        return plan;
    }

    private void ReadProductFields(
        Plan plan, TableRow row, PlannedProduct product, Func<TableRow, string, string> cell,
        Dictionary<string, int> categoryIds, Dictionary<string, int> categoryPaths, Dictionary<string, Brand> brands,
        Dictionary<string, Attr> attributes, Dictionary<int, Attr> specColumns)
    {
        // A product-level cell may be filled on every row of the product, but must then say the same thing everywhere.
        string? Field(string column, string? current, Func<string, string> normalize)
        {
            var value = normalize(cell(row, column));
            if (value.Length == 0)
            {
                return current;
            }

            if (current is not null && !string.Equals(current, value, StringComparison.Ordinal))
            {
                plan.Error(row.Number, column, T("api.import.e.product-differs", ("row", product.FirstRow)));
                return current;
            }

            return value;
        }

        var name = Field("product_name", product.Name, PersianText.Normalize);
        if (name is { Length: > Product.NameMaxLength })
        {
            plan.Error(row.Number, "product_name", T("api.import.e.name-too-long", ("max", Product.NameMaxLength)));
        }
        else
        {
            product.Name = name;
        }

        var category = Field("category", null, s => s);
        product.CategoryGiven |= category is not null;
        if (category is not null && product.CategoryId is null)
        {
            var normalizedPath = category.StartsWith('/') ? RoutePath.Normalize(category) : category;
            if (categoryPaths.TryGetValue(normalizedPath, out var byPath))
            {
                product.CategoryId = byPath;
            }
            else if (categoryIds.TryGetValue(category, out var bySlug))
            {
                product.CategoryId = bySlug;
            }
            else
            {
                plan.Error(row.Number, "category", T("api.import.e.category-unknown", ("value", category)));
            }
        }

        var brand = Field("brand", null, PersianText.Normalize);
        if (brand is not null && product.BrandId is null)
        {
            if (brands.TryGetValue(PersianText.SearchKey(brand), out var found))
            {
                product.BrandId = found.Id;
                product.BrandName = found.Name;
            }
            else
            {
                plan.Error(row.Number, "brand", T("api.import.e.brand-unknown", ("value", brand)));
            }
        }

        var status = Field("status", null, s => s.ToLowerInvariant());
        if (status is not null && product.Status is null)
        {
            product.Status = status switch
            {
                "draft" => ProductStatus.Draft,
                "published" => ProductStatus.Published,
                "archived" => ProductStatus.Archived,
                _ => null,
            };
            if (product.Status is null)
            {
                plan.Error(row.Number, "status", T("api.import.e.status-invalid"));
            }
        }

        var description = Field("description", product.Description, s => PersianText.NormalizeLetters(s));
        if (description is { Length: > Product.DescriptionMaxLength })
        {
            plan.Error(row.Number, "description", T("api.import.e.description-too-long", ("max", Product.DescriptionMaxLength)));
        }
        else
        {
            product.Description = description;
        }

        product.Path = Field("path", product.Path, s => s);

        // image ids (existing products only; checked once the group is complete)
        var images = cell(row, "image_ids");
        if (images.Length > 0 && product.ImageOrder is null)
        {
            if (images.Contains("http", StringComparison.OrdinalIgnoreCase) || images.Contains('/') || images.Contains('.'))
            {
                plan.Error(row.Number, "image_ids", T("api.import.e.image-url"));
            }
            else
            {
                var ids = images.Split([',', ';', '،', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(s => int.TryParse(ToAscii(s), NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : -1).ToList();
                if (ids.Any(id => id <= 0) || ids.Distinct().Count() != ids.Count)
                {
                    plan.Error(row.Number, "image_ids", T("api.import.e.image-ids-invalid"));
                }
                else
                {
                    product.ImageOrder = ids;
                }
            }
        }

        // spec:<code> columns: a value of the whole product
        foreach (var (index, attribute) in specColumns)
        {
            product.SpecColumns.Add(attribute.Id);
            var raw = index < row.Cells.Count ? Unsafe(row.Cells[index].Trim()) : string.Empty;
            if (raw.Length == 0)
            {
                continue;
            }

            if (ParseAttribute(plan, row.Number, SpecPrefix + attribute.Code, attribute, raw) is not { } value)
            {
                continue;
            }

            if (product.Spec.TryGetValue(attribute.Id, out var first) && first is not null && first != value)
            {
                plan.Error(row.Number, SpecPrefix + attribute.Code, T("api.import.e.product-differs", ("row", product.FirstRow)));
            }
            else
            {
                product.Spec[attribute.Id] = value;
            }
        }
    }

    private void ReadVariant(
        Plan plan, TableRow row, PlannedProduct product, string sku, Func<TableRow, string, string> cell, Func<string, bool> has,
        Dictionary<string, string> skuOwner, Dictionary<string, Attr> attributes, Dictionary<int, Attr> axisColumns)
    {
        var variant = new PlannedVariant(row.Number, sku);
        var ok = true;

        if (skuOwner.TryGetValue(sku, out var owner) && (product.IsNew || owner != product.Slug))
        {
            plan.Error(row.Number, "sku", T("api.import.e.sku-other-product", ("product", owner)));
            ok = false;
        }
        else if (product.Existing is not null)
        {
            variant.Existing = product.Existing.Variants.FirstOrDefault(v => string.Equals(v.Sku, sku, StringComparison.OrdinalIgnoreCase));
        }

        if (ParseWhole(cell(row, "price")) is { } price && price is >= 1 and <= ProductVariant.MaxPrice)
        {
            variant.Price = price;
        }
        else
        {
            plan.Error(row.Number, "price", T("api.import.e.price-invalid", ("max", ProductVariant.MaxPrice)));
            ok = false;
        }

        var compare = cell(row, "compare_at_price");
        if (compare.Length > 0)
        {
            if (ParseWhole(compare) is { } c && c > variant.Price && c <= ProductVariant.MaxPrice)
            {
                variant.CompareAt = c;
            }
            else if (ok)
            {
                plan.Error(row.Number, "compare_at_price", T("api.import.e.compare-invalid"));
                ok = false;
            }
        }

        if (has("stock"))
        {
            var stock = cell(row, "stock");
            if (ParseWhole(stock) is { } s && s is >= 0 and <= ProductVariant.MaxStock)
            {
                variant.Stock = (int)s;
            }
            else
            {
                plan.Error(row.Number, "stock", T("api.import.e.stock-invalid", ("max", ProductVariant.MaxStock)));
                ok = false;
            }
        }

        if (has("variant_active"))
        {
            var active = ToAscii(cell(row, "variant_active")).ToLowerInvariant();
            if (active.Length == 0)
            {
                variant.Active = true;
            }
            else if (active is "1" or "yes" or "true" or "y")
            {
                variant.Active = true;
            }
            else if (active is "0" or "no" or "false" or "n")
            {
                variant.Active = false;
            }
            else
            {
                plan.Error(row.Number, "variant_active", T("api.import.e.active-invalid"));
                ok = false;
            }
        }

        if (has("sort_order"))
        {
            var sort = cell(row, "sort_order");
            if (sort.Length == 0)
            {
                variant.Sort = 0;
            }
            else if (ParseWhole(sort) is { } n && n is >= 0 and <= 10_000)
            {
                variant.Sort = (int)n;
            }
            else
            {
                plan.Error(row.Number, "sort_order", T("api.import.e.sort-invalid"));
                ok = false;
            }
        }

        foreach (var (index, attribute) in axisColumns)
        {
            product.AxisColumns.Add(attribute.Id);
            var raw = index < row.Cells.Count ? Unsafe(row.Cells[index].Trim()) : string.Empty;
            variant.Axis[attribute.Id] = raw.Length == 0 ? null : ParseAttribute(plan, row.Number, AxisPrefix + attribute.Code, attribute, raw);
        }

        if (ok)
        {
            product.Variants.Add(variant);
        }
    }

    /// <summary>An option attribute takes the option's label or slug; a text attribute takes any text. Null (and an error) for anything else.</summary>
    private AttrValue? ParseAttribute(Plan plan, int row, string column, Attr attribute, string raw)
    {
        if (attribute.Type == AttributeType.Text)
        {
            var text = PersianText.Normalize(raw);
            if (text.Length > ProductAttributeValue.TextMaxLength)
            {
                plan.Error(row, column, T("api.import.e.attr-text-long", ("max", ProductAttributeValue.TextMaxLength)));
                return null;
            }

            return new AttrValue(null, text);
        }

        var key = PersianText.SearchKey(raw);
        var option = attribute.Options.FirstOrDefault(o => PersianText.SearchKey(o.Value) == key) ?? attribute.Options.FirstOrDefault(o => string.Equals(o.Slug, raw, StringComparison.OrdinalIgnoreCase));
        if (option is null)
        {
            plan.Error(row, column, T("api.import.e.attr-option-unknown", ("value", raw)));
            return null;
        }

        return new AttrValue(option.Id, null);
    }

    /// <summary>The same rule SetAttributes enforces: an attribute is either a spec of the whole product or a per-variant axis, not both.</summary>
    private void ValidateAttributeRules(Plan plan, PlannedProduct product, Dictionary<int, string> codes)
    {
        // The attribute values the product would have after the import: what exists, minus what the file replaces, plus what it sets.
        var existing = product.Existing?.AttributeValues ?? [];
        var inFile = product.Variants.Where(v => v.Existing is not null).Select(v => v.Existing!.Id).ToHashSet();
        var after = new List<(int Attribute, bool IsSpec)>();
        after.AddRange(existing
            .Where(v => !(v.VariantId is null ? product.SpecColumns.Contains(v.AttributeId) : product.AxisColumns.Contains(v.AttributeId) && inFile.Contains(v.VariantId.Value)))
            .Select(v => (v.AttributeId, v.VariantId is null)));
        after.AddRange(product.Spec.Where(x => x.Value is not null).Select(x => (x.Key, true)));
        foreach (var variant in product.Variants)
        {
            after.AddRange(variant.Axis.Where(x => x.Value is not null).Select(x => (x.Key, false)));
        }

        foreach (var group in after.GroupBy(x => x.Attribute).Where(g => g.Any(x => x.IsSpec) && g.Any(x => !x.IsSpec)))
        {
            plan.Error(product.FirstRow, SpecPrefix + codes.GetValueOrDefault(group.Key, "?"), T("api.import.e.attr-spec-axis", ("code", codes.GetValueOrDefault(group.Key, "?"))));
        }
    }

    private void ValidateImages(Plan plan, PlannedProduct product)
    {
        if (product.ImageOrder is null)
        {
            return;
        }

        if (product.IsNew)
        {
            plan.Error(product.FirstRow, "image_ids", T("api.import.e.image-new-product"));
            return;
        }

        var own = product.Existing!.Images.Select(i => i.Id).ToHashSet();
        if (product.ImageOrder.Any(id => !own.Contains(id)))
        {
            plan.Error(product.FirstRow, "image_ids", T("api.import.e.image-not-owned"));
        }
        else if (!own.SetEquals(product.ImageOrder))
        {
            plan.Error(product.FirstRow, "image_ids", T("api.import.e.image-set-differs"));
        }
    }

    /// <summary>Decides what each variant row does and whether the product itself changes (for the preview and the counts).</summary>
    private static void ClassifyVariants(PlannedProduct product)
    {
        if (product.Existing is { } p)
        {
            var changed = (product.Name is not null && product.Name != p.Name)
                || (product.CategoryId is { } c && c != p.CategoryId)
                || (product.BrandId is { } b && b != p.BrandId)
                || (product.Status is { } s && s != p.Status)
                || (product.Description is not null && product.Description != p.Description)
                || (product.ImageOrder is { } order && !order.SequenceEqual(p.Images.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).Select(i => i.Id)))
                || SpecChanged(product, p);
            product.ProductChanged = changed;
        }

        foreach (var variant in product.Variants)
        {
            if (variant.Existing is not { } v)
            {
                variant.Action = "create-variant";
                continue;
            }

            var axisChanged = product.AxisColumns.Any(a =>
            {
                var current = product.Existing!.AttributeValues.FirstOrDefault(x => x.AttributeId == a && x.VariantId == v.Id);
                var wanted = variant.Axis.GetValueOrDefault(a);
                return (current is null) != (wanted is null) || (current is not null && wanted is not null && (current.OptionId != wanted.OptionId || current.Text != wanted.Text));
            });
            variant.Action = variant.Price != v.Price || variant.CompareAt != v.CompareAtPrice || (variant.Stock is { } st && st != v.Stock)
                || (variant.Active is { } act && act != v.IsActive) || (variant.Sort is { } so && so != v.SortOrder) || axisChanged
                ? "update-variant" : "unchanged";
        }
    }

    private static bool SpecChanged(PlannedProduct product, Product existing) =>
        product.SpecColumns.Any(a =>
        {
            var current = existing.AttributeValues.FirstOrDefault(x => x.AttributeId == a && x.VariantId is null);
            var wanted = product.Spec.GetValueOrDefault(a);
            return (current is null) != (wanted is null) || (current is not null && wanted is not null && (current.OptionId != wanted.OptionId || current.Text != wanted.Text));
        });

    // ---- apply -------------------------------------------------------------------------------------------------------------------

    private async Task ApplyPlanAsync(Plan plan, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var created = new List<(Product Product, PlannedProduct Planned)>();
        foreach (var planned in plan.Products)
        {
            if (planned.Existing is { } product)
            {
                ApplyToExisting(product, planned, now);
            }
            else
            {
                var fresh = Product.Create(planned.CategoryId!.Value, planned.BrandId, planned.BrandName, planned.Name!, planned.Slug, planned.Description, planned.Status ?? ProductStatus.Draft, now);
                fresh.Variants.AddRange(planned.Variants.Select(v => ProductVariant.Create(v.Sku, v.Price, v.CompareAt, v.Stock ?? 0, v.Active ?? true, v.Sort ?? 0)));
                db.Products.Add(fresh);
                created.Add((fresh, planned));
            }
        }

        await db.SaveChangesAsync(ct); // new products and variants get their ids

        foreach (var (product, variant, planned) in pendingAxis)
        {
            AddAxisValues(product, variant.Id, planned);
        }

        foreach (var (product, planned) in created)
        {
            foreach (var (variant, plannedVariant) in product.Variants.Zip(planned.Variants))
            {
                AddAxisValues(product, variant.Id, plannedVariant);
            }

            foreach (var (attributeId, value) in planned.Spec.Where(x => x.Value is not null))
            {
                product.AttributeValues.Add(Make(attributeId, value!, null));
            }

            var route = await routes.AddForNewAsync(RouteTargetTypes.Product, product.Id, product.Slug, planned.Path, ct);
            if (!route.IsSuccess)
            {
                throw new DbUpdateException("The product's path was taken meanwhile.");
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static ProductAttributeValue Make(int attributeId, AttrValue value, int? variantId) =>
        value.OptionId is { } option ? ProductAttributeValue.ForOption(attributeId, option, variantId) : ProductAttributeValue.ForText(attributeId, value.Text!, variantId);

    private static void AddAxisValues(Product product, int variantId, PlannedVariant planned)
    {
        foreach (var (attributeId, value) in planned.Axis.Where(x => x.Value is not null))
        {
            product.AttributeValues.Add(Make(attributeId, value!, variantId));
        }
    }

    private void ApplyToExisting(Product product, PlannedProduct planned, DateTime now)
    {
        var brandId = planned.BrandId ?? product.BrandId;
        var brandName = planned.BrandId is not null ? planned.BrandName : db.Brands.Where(b => b.Id == brandId).Select(b => b.Name).SingleOrDefault();
        if (planned.ProductChanged)
        {
            product.Update(planned.CategoryId ?? product.CategoryId, brandId, brandName, planned.Name ?? product.Name, planned.Description ?? product.Description, planned.Status ?? product.Status, now);
        }

        foreach (var attributeId in planned.SpecColumns)
        {
            foreach (var old in product.AttributeValues.Where(v => v.AttributeId == attributeId && v.VariantId is null).ToList())
            {
                product.AttributeValues.Remove(old);
            }

            if (planned.Spec.GetValueOrDefault(attributeId) is { } spec)
            {
                product.AttributeValues.Add(Make(attributeId, spec, null));
            }
        }

        foreach (var variant in planned.Variants)
        {
            if (variant.Existing is { } existing)
            {
                var before = existing.Stock;
                existing.Update(existing.Sku, variant.Price, variant.CompareAt, variant.Stock ?? existing.Stock, variant.Active ?? existing.IsActive, variant.Sort ?? existing.SortOrder);
                if (existing.Stock != before)
                {
                    db.StockAdjustments.Add(StockAdjustment.Create(existing.Id, currentUser.UserId, existing.Stock - before, existing.Stock, null, StockAdjustment.ImportSource, now));
                }

                foreach (var attributeId in planned.AxisColumns)
                {
                    foreach (var old in product.AttributeValues.Where(v => v.AttributeId == attributeId && v.VariantId == existing.Id).ToList())
                    {
                        product.AttributeValues.Remove(old);
                    }

                    if (variant.Axis.GetValueOrDefault(attributeId) is { } axis)
                    {
                        product.AttributeValues.Add(Make(attributeId, axis, existing.Id));
                    }
                }
            }
            else
            {
                // A new variant of an existing product: its axis values are added after its id exists (see below).
                var fresh = ProductVariant.Create(variant.Sku, variant.Price, variant.CompareAt, variant.Stock ?? 0, variant.Active ?? true, variant.Sort ?? 0);
                product.Variants.Add(fresh);
                pendingAxis.Add((product, fresh, variant));
            }
        }

        if (planned.ImageOrder is { } order)
        {
            for (var i = 0; i < order.Count; i++)
            {
                product.Images.Single(image => image.Id == order[i]).MoveTo(i);
            }
        }

        product.Touch(now);
    }

    private readonly List<(Product Product, ProductVariant Variant, PlannedVariant Planned)> pendingAxis = [];

    // ---- numbers -----------------------------------------------------------------------------------------------------------------

    private static string ToAscii(string value) => new(value.Select(c => c switch
    {
        >= '۰' and <= '۹' => (char)('0' + (c - '۰')),
        >= '٠' and <= '٩' => (char)('0' + (c - '٠')),
        _ => c,
    }).ToArray());

    /// <summary>A whole number; Persian digits and thousands separators (, ٬ space) are accepted. Null for anything else.</summary>
    private static long? ParseWhole(string value)
    {
        var text = ToAscii(value).Replace(",", string.Empty).Replace("\u066C", string.Empty).Replace(" ", string.Empty); // \u066C is the Arabic thousands separator
        return long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;
    }
}
