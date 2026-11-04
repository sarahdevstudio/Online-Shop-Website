using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shop.Application.Routing;
using Shop.Domain.Blog;
using Shop.Domain.Content;
using Shop.Domain.Routing;
using Shop.Infrastructure.Catalog;
using Shop.Infrastructure.Persistence;
using Shop.Infrastructure.Routing;

namespace Shop.IntegrationTests;

/// <summary>
/// Turning dynamic routing on moves nothing (docs/11 C.1 #3): on its own database, the Development sample catalog, the info
/// pages and some blog posts are seeded exactly as a first start does, then EVERY category, product, info page and post must
/// resolve at the URL it has always had, and must keep resolving after a second run.
/// </summary>
public sealed class RouteSeedTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task EveryExistingPublicUrl_ResolvesUnchanged_AfterTheBackfill_AndTheBackfillIsIdempotent()
    {
        var connection = ApiFixture.NewDatabaseConnectionString();
        var options = new DbContextOptionsBuilder<ShopDbContext>().UseSqlServer(connection).Options;
        await using (var setup = new ShopDbContext(options))
        {
            await setup.Database.MigrateAsync();
        }

        try
        {
            var expected = new List<(string Type, int Id, string Slug, string Url)>();
            await using (var db = new ShopDbContext(options))
            {
                var seeder = new CatalogSeeder(db, TimeProvider.System, NullLogger<CatalogSeeder>.Instance);
                await seeder.EnsureAttributesAsync(CancellationToken.None);
                await seeder.SeedDevelopmentDataAsync(CancellationToken.None);
                db.ContentPages.AddRange(ContentSlugs.All.Select(slug => ContentPage.Create(slug, "عنوان " + slug, "متن", DateTime.UtcNow)));
                db.BlogPosts.AddRange(
                    BlogPost.Create("led-vs-halogen", "راهنما", "خلاصه", "متن", true, null, DateTime.UtcNow),
                    BlogPost.Create("color-temperature", "راهنما", "خلاصه", "متن", true, null, DateTime.UtcNow),
                    BlogPost.Create("a-draft-post", "پیش‌نویس", "خلاصه", "متن", false, null, DateTime.UtcNow));
                await db.SaveChangesAsync();

                expected.AddRange((await db.Categories.ToListAsync()).Select(c => (RouteTargetTypes.Category, c.Id, c.Slug, $"/category/{c.Slug}")));
                expected.AddRange((await db.Products.ToListAsync()).Select(p => (RouteTargetTypes.Product, p.Id, p.Slug, $"/product/{p.Slug}")));
                expected.AddRange((await db.ContentPages.ToListAsync()).Select(p => (RouteTargetTypes.ContentPage, p.Id, p.Slug, $"/{p.Slug}")));
                expected.AddRange((await db.BlogPosts.ToListAsync()).Select(p => (RouteTargetTypes.BlogPost, p.Id, p.Slug, $"/blog/{p.Slug}")));
            }

            Assert.Equal(5, expected.Count(e => e.Type == RouteTargetTypes.Category));
            Assert.Equal(20, expected.Count(e => e.Type == RouteTargetTypes.Product));
            Assert.Equal(ContentSlugs.All.Count, expected.Count(e => e.Type == RouteTargetTypes.ContentPage));
            Assert.Equal(3, expected.Count(e => e.Type == RouteTargetTypes.BlogPost));

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(TimeProvider.System);
            services.AddDbContext<ShopDbContext>(o => o.UseSqlServer(connection));
            services.AddSingleton<RouteTable>();
            services.AddScoped<RouteSeeder>();
            await using var provider = services.BuildServiceProvider();
            var table = provider.GetRequiredService<RouteTable>();

            for (var run = 0; run < 2; run++)
            {
                await using var scope = provider.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<RouteSeeder>().EnsureAsync(CancellationToken.None);

                foreach (var (type, id, slug, url) in expected)
                {
                    var resolved = table.Resolve(url);
                    Assert.Equal((type, id, slug, url), (resolved.Kind, resolved.Id ?? -1, resolved.Slug, resolved.Path));
                    Assert.Equal(url, table.PathOf(type, id, slug)); // links built from the table are the same URLs
                }
            }

            await using var check = new ShopDbContext(options);
            Assert.Equal(expected.Count, await check.RoutePaths.CountAsync()); // one row each, nothing duplicated by the second run
            Assert.True(await check.RoutePaths.AllAsync(r => r.IsCurrent && !r.IsGone));
            Assert.Equal(RouteResolutionDto.NotFound, table.Resolve("/category/not-in-the-seed").Kind);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await using var cleanup = new ShopDbContext(options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }
}
