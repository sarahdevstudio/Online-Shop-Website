using System.Text.Json;
using System.Text.Json.Serialization;
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
/// means nothing is written.
/// </summary>
public static class ImportInitialCatalogCommand
{
    public const string Usage = "Usage: import-initial-catalog [--dry-run]";

    private sealed record ManifestCategory(string Slug, string Name, string? ParentSlug);
    private sealed record ManifestImage(string File, string Alt);
    private sealed record ManifestProduct(
        string SourceUrl, string Slug, string Name, string CategorySlug, long? PriceToman,
        Dictionary<string, string> Specs, string Description, List<ManifestImage> Images);
    private sealed record Manifest(List<ManifestCategory> Categories, List<ManifestProduct> Products);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
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

            var now = clock.GetUtcNow().UtcDateTime;
            foreach (var manifestProduct in productsToCreate)
            {
                var category = categoriesBySlug[manifestProduct.CategorySlug];
                var description = Describe(manifestProduct);
                var product = Product.Create(
                    category.Id, brandId: null, brandName: null, manifestProduct.Name, manifestProduct.Slug, description, ProductStatus.Draft, now);
                product.Variants.Add(ProductVariant.Create(
                    manifestProduct.Slug, manifestProduct.PriceToman ?? 0, compareAtPrice: null, stock: 0, isActive: true, sortOrder: 0));
                db.Products.Add(product);
                await db.SaveChangesAsync();

                for (var i = 0; i < manifestProduct.Images.Count; i++)
                {
                    var manifestImage = manifestProduct.Images[i];
                    var sourcePath = Path.Combine(catalogDir, "images", manifestImage.File);
                    await using var fileStream = File.OpenRead(sourcePath);
                    var storedPath = await media.SaveAsync(fileStream, $"products/{product.Id}", ".webp", CancellationToken.None);
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
            throw;
        }

        // The route table is seeded once at API startup; entities created afterwards (here) need the same
        // backfill RouteSeeder already does for exactly this case ("heals an entity created by a path that
        // skipped the route service"), or their storefront page 404s despite existing.
        await scope.ServiceProvider.GetRequiredService<Shop.Infrastructure.Routing.RouteSeeder>().EnsureAsync(CancellationToken.None);

        Console.WriteLine($"Created {categoriesToCreate.Count} categories and {productsToCreate.Count} products.");
        return 0;
    }

    private static string Describe(ManifestProduct product)
    {
        var specLines = product.Specs.Select(s => $"{s.Key}: {s.Value}");
        return string.Join('\n', new[] { product.Description }.Concat(specLines).Where(line => line.Length > 0));
    }
}
