using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Shop.Application.Catalog;
using Shop.Domain.Catalog;
using Shop.Infrastructure;
using Shop.Infrastructure.Catalog;
using Shop.Infrastructure.Persistence;

namespace Shop.Api.Commands;

/// <summary>
/// <c>dotnet Shop.Api.dll import-initial-catalog [--dry-run]</c>: reads the manifest and images that
/// <c>tools/catalog-scrape</c> produced (never the network) and creates the categories and products that don't
/// already exist, matching by slug. Existing ones are never touched (admin edits win). One transaction: any error
/// means nothing is written (including the image files already saved during this run).
/// </summary>
public static class ImportInitialCatalogCommand
{
    public const string Usage = "Usage: import-initial-catalog [--dry-run]";

    private sealed record ManifestCategory(string Slug, string Name, string? ParentSlug);
    private sealed record ManifestImage(string File, string Alt);
    private sealed record ManifestProduct(
        string SourceUrl, string Slug, string Sku, string Name, string CategorySlug, long? PriceToman,
        Dictionary<string, string> Specs, string Description, List<ManifestImage> Images);
    private sealed record Manifest(List<ManifestCategory> Categories, List<ManifestProduct> Products);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// A scraped spec maps to an existing or new attribute, as a plain spec of the whole product (never a variant
    /// axis: the source has no variants). <see cref="Reader.Invoke"/> turns the raw spec text into either a Text
    /// value, or an (option value, option slug) pair for an Option attribute; returning null skips that product's
    /// value for that key, rather than writing one the attribute model can't represent cleanly.
    /// </summary>
    private sealed record SpecAttribute(string Code, string? Name, AttributeType Type, Func<string, (string? Text, (string Value, string Slug)? Option)> Reader);

    private static (string Value, string Slug)? ExtractBase(string raw)
    {
        var match = Regex.Match(raw, "E14|E27|GU10", RegexOptions.IgnoreCase);
        return match.Success ? (match.Value.ToUpperInvariant(), match.Value.ToLowerInvariant()) : null;
    }

    private static (string Value, string Slug)? ExtractWattage(string raw)
    {
        var match = Regex.Match(raw, @"\d+");
        return match.Success ? ($"{match.Value} وات", $"{match.Value}w") : null;
    }

    // Reused attributes (arms, finish, lumen, base, wattage) already exist: EnsureAttributesAsync seeds them in
    // every environment. New ones (dimensions, weight, style, warranty) have no match in the lighting-attribute
    // reference data and are created the first time this import needs them.
    private static readonly Dictionary<string, SpecAttribute> SpecMap = new()
    {
        ["تعداد شاخه"] = new("arms", null, AttributeType.Text, raw => (raw, null)),
        ["رنگ"] = new("finish", null, AttributeType.Text, raw => (raw, null)),
        ["رنگ نور"] = new("lumen", null, AttributeType.Text, raw => (Regex.Match(raw, @"\d+") is { Success: true } m ? m.Value : raw, null)),
        ["نوع لامپ"] = new("base", null, AttributeType.Option, raw => (null, ExtractBase(raw))),
        ["میزان-مصرف"] = new("wattage", null, AttributeType.Option, raw => (null, ExtractWattage(raw))),
        ["ابعاد"] = new("dimensions", "ابعاد", AttributeType.Text, raw => (raw, null)),
        ["وزن"] = new("weight", "وزن", AttributeType.Text, raw => (raw, null)),
        ["سبک"] = new("style", "سبک", AttributeType.Text, raw => (raw, null)),
        ["گارانتی"] = new("warranty", "گارانتی", AttributeType.Text, raw => (raw, null)),
    };

    public static async Task<int> RunAsync(string[] args)
    {
        var dryRun = false;
        foreach (var arg in args)
        {
            if (arg != "--dry-run")
            {
                Console.Error.WriteLine($"Unknown argument '{arg}'.");
                Console.Error.WriteLine(Usage);
                return 2;
            }

            dryRun = true;
        }

        // Overridable so integration tests can point it at a small fixture instead of the real scraped catalog.
        var catalogDir = Environment.GetEnvironmentVariable("CATALOG_IMPORT_DIR")
            ?? Path.Combine(AppContext.BaseDirectory, "SeedData", "initial-catalog");
        var manifestPath = Path.Combine(catalogDir, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            Console.Error.WriteLine($"No manifest at {manifestPath}. Run tools/catalog-scrape/scrape.mjs first.");
            return 1;
        }

        var manifest = JsonSerializer.Deserialize<Manifest>(await File.ReadAllTextAsync(manifestPath), JsonOptions)
            ?? throw new InvalidOperationException("Empty manifest.");

        var skus = manifest.Products.Select(p => SanitizeSku(p.Sku)).ToList();
        var duplicateSku = skus.GroupBy(s => s, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicateSku is not null)
        {
            Console.Error.WriteLine($"Two products share the SKU '{duplicateSku.Key}'. Fix the manifest and re-run.");
            return 1;
        }

        var builder = WebApplication.CreateBuilder([]);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Host.UseDefaultServiceProvider(options =>
        {
            options.ValidateOnBuild = false;
            options.ValidateScopes = false;
        });
        builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
        await using var app = builder.Build();
        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(CancellationToken.None);

        var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
        var media = scope.ServiceProvider.GetRequiredService<IMediaStorage>();
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var existingCategorySlugs = await db.Categories.Select(c => c.Slug).ToListAsync();
        var existingProductSlugs = await db.Products.Select(p => p.Slug).ToHashSetAsync();

        var categoriesToCreate = manifest.Categories.Where(c => !existingCategorySlugs.Contains(c.Slug)).ToList();
        var productsToCreate = manifest.Products.Where(p => !existingProductSlugs.Contains(p.Slug)).OrderBy(p => p.Slug).ToList();
        var productsToSkip = manifest.Products.Count - productsToCreate.Count;

        Console.WriteLine($"Categories: {categoriesToCreate.Count} to create, {manifest.Categories.Count - categoriesToCreate.Count} already exist.");
        Console.WriteLine($"Products: {productsToCreate.Count} to create, {productsToSkip} already exist (skipped).");
        foreach (var product in productsToCreate)
        {
            Console.WriteLine($"  create {product.Slug} ({product.Images.Count} images, {product.PriceToman?.ToString() ?? "no price"} Toman)");
        }

        if (dryRun)
        {
            Console.WriteLine("Dry run: nothing written.");
            return 0;
        }

        var savedMediaPaths = new List<string>();
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var categoriesBySlug = await db.Categories.ToDictionaryAsync(c => c.Slug, c => c);
            foreach (var manifestCategory in categoriesToCreate)
            {
                var parentId = manifestCategory.ParentSlug is { } parentSlug ? categoriesBySlug[parentSlug].Id : (int?)null;
                var category = Category.Create(manifestCategory.Name, manifestCategory.Slug, parentId, description: null, sortOrder: 10, isActive: false);
                db.Categories.Add(category);
                await db.SaveChangesAsync();
                categoriesBySlug[manifestCategory.Slug] = category;
            }

            var attributesByCode = await db.Attributes.Include(a => a.Options).ToDictionaryAsync(a => a.Code);
            var nextAttributeSort = attributesByCode.Count == 0 ? 10 : attributesByCode.Values.Max(a => a.SortOrder) + 10;

            var now = clock.GetUtcNow().UtcDateTime;
            foreach (var manifestProduct in productsToCreate)
            {
                var category = categoriesBySlug[manifestProduct.CategorySlug];
                var product = Product.Create(
                    category.Id, brandId: null, brandName: null, manifestProduct.Name, manifestProduct.Slug,
                    manifestProduct.Description, ProductStatus.Draft, now);
                product.Variants.Add(ProductVariant.Create(
                    SanitizeSku(manifestProduct.Sku), manifestProduct.PriceToman ?? 0, compareAtPrice: null, stock: 0, isActive: true, sortOrder: 0));
                db.Products.Add(product);
                await db.SaveChangesAsync();

                foreach (var (key, raw) in manifestProduct.Specs)
                {
                    if (!SpecMap.TryGetValue(key, out var spec))
                    {
                        continue;
                    }

                    if (!attributesByCode.TryGetValue(spec.Code, out var attribute))
                    {
                        attribute = AttributeDefinition.Create(spec.Code, spec.Name!, unit: null, spec.Type, isFilterable: false, nextAttributeSort);
                        nextAttributeSort += 10;
                        db.Attributes.Add(attribute);
                        await db.SaveChangesAsync();
                        attributesByCode[spec.Code] = attribute;
                    }

                    var (text, option) = spec.Reader(raw);
                    if (attribute.Type == AttributeType.Text && text is { Length: > 0 })
                    {
                        product.AttributeValues.Add(ProductAttributeValue.ForText(attribute.Id, text, variantId: null));
                    }
                    else if (attribute.Type == AttributeType.Option && option is { } chosen)
                    {
                        var attrOption = attribute.Options.FirstOrDefault(o => o.Slug == chosen.Slug);
                        if (attrOption is null)
                        {
                            attrOption = AttributeOption.Create(chosen.Value, chosen.Slug, attribute.Options.Count * 10 + 10);
                            attribute.Options.Add(attrOption);
                            await db.SaveChangesAsync();
                        }

                        product.AttributeValues.Add(ProductAttributeValue.ForOption(attribute.Id, attrOption.Id, variantId: null));
                    }
                    // Else: the raw value didn't fit the attribute (e.g. no recognizable socket code) — skipped.
                }

                for (var i = 0; i < manifestProduct.Images.Count; i++)
                {
                    var manifestImage = manifestProduct.Images[i];
                    var sourcePath = Path.Combine(catalogDir, "images", manifestImage.File);
                    await using var fileStream = File.OpenRead(sourcePath);
                    var storedPath = await media.SaveAsync(fileStream, $"products/{product.Id}", ".webp", CancellationToken.None);
                    savedMediaPaths.Add(storedPath);
                    product.Images.Add(ProductImage.Create(storedPath, manifestImage.Alt, i));
                }

                await db.SaveChangesAsync();
            }

            db.CatalogTransferLogs.Add(CatalogTransferLog.Create(
                null, CatalogTransferLog.ImportKind, "initial-catalog/manifest.json",
                manifest.Products.Count, productsToCreate.Count, 0, productsToCreate.Count, 0, now));
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            foreach (var path in savedMediaPaths)
            {
                await media.DeleteAsync(path, CancellationToken.None);
            }

            throw;
        }

        // The route table is seeded once at API startup; entities created afterwards (here) need the same
        // backfill RouteSeeder already does for exactly this case ("heals an entity created by a path that
        // skipped the route service"), or their storefront page 404s despite existing.
        await scope.ServiceProvider.GetRequiredService<Shop.Infrastructure.Routing.RouteSeeder>().EnsureAsync(CancellationToken.None);

        Console.WriteLine($"Created {categoriesToCreate.Count} categories and {productsToCreate.Count} products.");
        return 0;
    }

    /// <summary>The site's product code may contain '/' (e.g. "H18/12"), which <see cref="ProductVariant.IsValidSku"/> refuses.</summary>
    private static string SanitizeSku(string sku) => sku.Replace('/', '-');
}
