using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shop.Domain.Catalog;
using Shop.Infrastructure.Catalog;
using Shop.Infrastructure.Persistence;

namespace Shop.IntegrationTests;

/// <summary>Runs the seeder against its own empty database (the shared test database is not Development).</summary>
public sealed class CatalogSeedTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task DevelopmentSeed_CreatesTheSampleCatalog_AndIsIdempotent()
    {
        var options = new DbContextOptionsBuilder<ShopDbContext>().UseSqlServer(ApiFixture.NewDatabaseConnectionString()).Options;
        await using (var setup = new ShopDbContext(options))
        {
            await setup.Database.MigrateAsync();
        }

        try
        {
            for (var run = 0; run < 2; run++)
            {
                await using var db = new ShopDbContext(options);
                var seeder = new CatalogSeeder(db, TimeProvider.System, NullLogger<CatalogSeeder>.Instance);
                await seeder.EnsureAttributesAsync(CancellationToken.None);
                await seeder.SeedDevelopmentDataAsync(CancellationToken.None);
            }

            await using var check = new ShopDbContext(options);
            Assert.Equal(5, await check.Categories.CountAsync());
            Assert.Equal(2, await check.Categories.CountAsync(c => c.ParentId != null));
            Assert.Equal(5, await check.Brands.CountAsync());
            Assert.Equal(20, await check.Products.CountAsync());
            Assert.Equal(1, await check.Products.CountAsync(p => p.Status == ProductStatus.Draft));
            Assert.Equal(1, await check.Products.CountAsync(p => p.Status == ProductStatus.Archived));
            Assert.Equal(1, await check.Products.CountAsync(p => p.BrandId == null));
            Assert.True(await check.ProductVariants.AnyAsync(v => v.Stock == 0));
            Assert.Equal(CatalogSeeder.AttributeCodes.Count, await check.Attributes.CountAsync());
            Assert.Equal(4, await check.AttributeOptions.CountAsync(o => check.Attributes.Any(a => a.Id == o.AttributeId && a.Code == "cct")));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await using var cleanup = new ShopDbContext(options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task NonDevelopmentHost_HasAttributes_ButNoSampleProducts()
    {
        var (attributes, sampleProducts) = await Fixture.WithDbAsync(async db =>
            (await db.Attributes.CountAsync(), await db.Products.CountAsync(p => p.Slug == "pars-shahab-led-bulb-e27")));

        Assert.Equal(CatalogSeeder.AttributeCodes.Count, attributes);
        Assert.Equal(0, sampleProducts);
    }
}
