using Microsoft.EntityFrameworkCore;
using Shop.Api.Commands;
using Shop.Domain.Catalog;
using Shop.Infrastructure.Persistence;

namespace Shop.IntegrationTests;

/// <summary>
/// <c>import-initial-catalog</c> against a real throwaway database, with a tiny fixture manifest (never the real
/// scraped catalog, never the network).
/// </summary>
public sealed class ImportInitialCatalogCommandTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private sealed class EnvScope : IDisposable
    {
        private readonly Dictionary<string, string?> _previous = [];

        public EnvScope(params (string Name, string? Value)[] variables)
        {
            foreach (var (name, value) in variables)
            {
                _previous[name] = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, value);
            }
        }

        public void Dispose()
        {
            foreach (var (name, value) in _previous)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }

    /// <summary>A fixture catalog directory with one real (small) image reused for every product, from the committed data.</summary>
    private static string WriteFixtureCatalog(string manifestJson)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"shop-catalog-import-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "images"));
        File.WriteAllText(Path.Combine(dir, "manifest.json"), manifestJson);

        var repoRoot = FindRepoRoot();
        var sourceImage = Path.Combine(repoRoot, "src", "Api", "SeedData", "initial-catalog", "images", "h12-1.webp");
        File.Copy(sourceImage, Path.Combine(dir, "images", "fixture-1.webp"));
        return dir;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PROGRESS.md")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not find the repository root.");
    }

    private const string ManifestTemplate = """
    {
      "categories": [ { "slug": "test-import-cat", "name": "دسته آزمایشی", "parentSlug": null } ],
      "products": [
        { "sourceUrl": "https://example.test/a", "slug": "test-import-a", "name": "محصول آزمایشی A",
          "categorySlug": "test-import-cat", "priceToman": 2800000, "specs": { "وزن": "4 kg" },
          "description": "توضیح A", "images": [ { "file": "fixture-1.webp", "alt": "محصول آزمایشی A" } ] },
        { "sourceUrl": "https://example.test/b", "slug": "test-import-b", "name": "محصول آزمایشی B",
          "categorySlug": "test-import-cat", "priceToman": 1500000, "specs": {}, "description": "توضیح B",
          "images": [ { "file": "fixture-1.webp", "alt": "محصول آزمایشی B" } ] }
      ]
    }
    """;

    private const string InvalidManifest = """
    {
      "categories": [ { "slug": "test-import-cat", "name": "دسته آزمایشی", "parentSlug": null } ],
      "products": [
        { "sourceUrl": "https://example.test/a", "slug": "test-import-a", "name": "محصول آزمایشی A",
          "categorySlug": "test-import-cat", "priceToman": 2800000, "specs": {},
          "description": "توضیح A", "images": [ { "file": "fixture-1.webp", "alt": "A" } ] },
        { "sourceUrl": "https://example.test/bad", "slug": "not a valid slug", "name": "محصول خراب",
          "categorySlug": "test-import-cat", "priceToman": 1000000, "specs": {}, "description": "",
          "images": [ { "file": "fixture-1.webp", "alt": "bad" } ] }
      ]
    }
    """;

    [Fact]
    public async Task DryRun_WritesNothing_ThenRealRun_CreatesTwoProducts_ThenRerun_CreatesNothing()
    {
        var connection = ApiFixture.NewDatabaseConnectionString();
        var media = Path.Combine(Path.GetTempPath(), $"shop-catalog-import-media-{Guid.NewGuid():N}");
        var catalogDir = WriteFixtureCatalog(ManifestTemplate);
        var options = new DbContextOptionsBuilder<ShopDbContext>().UseSqlServer(connection).Options;
        try
        {
            using (new EnvScope(
                ("ASPNETCORE_ENVIRONMENT", "Testing"),
                ("ConnectionStrings__Default", connection),
                ("Database__MigrateOnStartup", "true"),
                ("Media__RootPath", media),
                ("CATALOG_IMPORT_DIR", catalogDir),
                ("Seed__AdminEmail", null),
                ("Seed__AdminPassword", null)))
            {
                Assert.Equal(0, await ImportInitialCatalogCommand.RunAsync(["--dry-run"]));

                await using (var db = new ShopDbContext(options))
                {
                    Assert.Equal(0, await db.Products.CountAsync());
                    Assert.Equal(0, await db.Categories.CountAsync());
                }

                Assert.Equal(0, await ImportInitialCatalogCommand.RunAsync([]));

                await using (var db = new ShopDbContext(options))
                {
                    var products = await db.Products.Include(p => p.Variants).Include(p => p.Images).OrderBy(p => p.Slug).ToListAsync();
                    Assert.Equal(2, products.Count);
                    Assert.All(products, p => Assert.Equal(ProductStatus.Draft, p.Status));
                    Assert.All(products, p => Assert.Single(p.Images));
                    Assert.All(products, p => Assert.Equal(0, p.Variants.Single().Stock));

                    // The manifest's priceToman (already the source Rial price / 10) lands unchanged on the variant.
                    Assert.Equal(2_800_000, products.Single(p => p.Slug == "test-import-a").Variants.Single().Price);
                    Assert.Equal(1_500_000, products.Single(p => p.Slug == "test-import-b").Variants.Single().Price);

                    var category = await db.Categories.SingleAsync();
                    Assert.False(category.IsActive); // new categories are hidden until an admin publishes them

                    Assert.Equal(1, await db.CatalogTransferLogs.CountAsync());
                }

                // Re-run: idempotent, matches by slug, never overwrites.
                Assert.Equal(0, await ImportInitialCatalogCommand.RunAsync([]));
                await using (var db = new ShopDbContext(options))
                {
                    Assert.Equal(2, await db.Products.CountAsync());
                    Assert.Equal(1, await db.Categories.CountAsync());
                    Assert.Equal(2, await db.CatalogTransferLogs.CountAsync()); // every real run is logged, even a no-op one
                }
            }
        }
        finally
        {
            Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
            await using var cleanup = new ShopDbContext(options);
            await cleanup.Database.EnsureDeletedAsync();
            Directory.Delete(catalogDir, recursive: true);
            if (Directory.Exists(media))
            {
                Directory.Delete(media, recursive: true);
            }
        }
    }

    [Fact]
    public async Task InvalidManifestEntry_FailsTheWholeRun_NothingIsWritten()
    {
        var connection = ApiFixture.NewDatabaseConnectionString();
        var media = Path.Combine(Path.GetTempPath(), $"shop-catalog-import-media-{Guid.NewGuid():N}");
        var catalogDir = WriteFixtureCatalog(InvalidManifest);
        var options = new DbContextOptionsBuilder<ShopDbContext>().UseSqlServer(connection).Options;
        try
        {
            using (new EnvScope(
                ("ASPNETCORE_ENVIRONMENT", "Testing"),
                ("ConnectionStrings__Default", connection),
                ("Database__MigrateOnStartup", "true"),
                ("Media__RootPath", media),
                ("CATALOG_IMPORT_DIR", catalogDir),
                ("Seed__AdminEmail", null),
                ("Seed__AdminPassword", null)))
            {
                // The second product's slug ("not a valid slug") is invalid: the whole run fails, including the
                // first (valid) product, because it all runs in one transaction.
                await Assert.ThrowsAnyAsync<Exception>(() => ImportInitialCatalogCommand.RunAsync([]));

                await using var db = new ShopDbContext(options);
                Assert.Equal(0, await db.Products.CountAsync());
            }
        }
        finally
        {
            Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
            await using var cleanup = new ShopDbContext(options);
            await cleanup.Database.EnsureDeletedAsync();
            Directory.Delete(catalogDir, recursive: true);
            if (Directory.Exists(media))
            {
                Directory.Delete(media, recursive: true);
            }
        }
    }
}
