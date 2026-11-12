using Microsoft.EntityFrameworkCore;
using Shop.Api.Commands;
using Shop.Domain.Content;
using Shop.Infrastructure.Content;
using Shop.Infrastructure.Persistence;

namespace Shop.IntegrationTests;

/// <summary>
/// <c>import-initial-content</c> against a real throwaway database, with a tiny fixture manifest (never the real
/// scraped content, never the network).
/// </summary>
public sealed class ImportInitialContentCommandTests(ApiFixture fixture) : ApiTestBase(fixture)
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

    private static string WriteFixtureContent(string manifestJson)
    {
        var dir = Path.Combine(Path.GetTempPath(), $"shop-content-import-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(dir, "images"));
        File.WriteAllText(Path.Combine(dir, "manifest.json"), manifestJson);
        return dir;
    }

    private const string ManifestTemplate = """
    {
      "posts": [
        { "slug": "test-import-post-a", "title": "عنوان آزمایشی A", "excerpt": "خلاصهٔ آزمایشی A.",
          "body": "متن آزمایشی A.", "publishedAtUtc": "2019-09-01T00:00:00Z",
          "image": { "file": "a.webp", "alt": "عنوان آزمایشی A" } },
        { "slug": "test-import-post-b", "title": "عنوان آزمایشی B", "excerpt": "خلاصهٔ آزمایشی B.",
          "body": "متن آزمایشی B.", "publishedAtUtc": "2019-08-28T00:00:00Z",
          "image": { "file": "b.webp", "alt": "عنوان آزمایشی B" } }
      ]
    }
    """;

    [Fact]
    public async Task DryRun_WritesNothing_ThenRealRun_UpdatesUntouchedSeeds_KeepsEditedOnes_CreatesTwoPosts_ThenRerun_ChangesNothing()
    {
        var connection = ApiFixture.NewDatabaseConnectionString();
        var media = Path.Combine(Path.GetTempPath(), $"shop-content-import-media-{Guid.NewGuid():N}");
        var contentDir = WriteFixtureContent(ManifestTemplate);
        var options = new DbContextOptionsBuilder<ShopDbContext>().UseSqlServer(connection).Options;
        try
        {
            using (new EnvScope(
                ("ASPNETCORE_ENVIRONMENT", "Testing"),
                ("ConnectionStrings__Default", connection),
                ("Database__MigrateOnStartup", "true"),
                ("Media__RootPath", media),
                ("CONTENT_IMPORT_DIR", contentDir),
                ("Seed__AdminEmail", null),
                ("Seed__AdminPassword", null)))
            {
                // Seed once (About still the previous draft, Contact edited by the owner before this import runs).
                await using (var seedDb = new ShopDbContext(options))
                {
                    await seedDb.Database.MigrateAsync();
                    var now = DateTime.UtcNow;
                    seedDb.ContentPages.Add(ContentPage.Create(ContentSlugs.About, "دربارهٔ ما", ContentSeeds.PreviousAboutBody, now));
                    seedDb.ContentPages.Add(ContentPage.Create(ContentSlugs.Contact, "درباره شد توسط مالک", "متنی که مالک فروشگاه خودش نوشته است.", now));
                    await seedDb.SaveChangesAsync();
                }

                Assert.Equal(0, await ImportInitialContentCommand.RunAsync(["--dry-run"]));

                await using (var db = new ShopDbContext(options))
                {
                    var about = await db.ContentPages.SingleAsync(p => p.Slug == ContentSlugs.About);
                    Assert.Equal(ContentSeeds.PreviousAboutBody, about.Body); // untouched by the dry run
                    Assert.Equal(0, await db.BlogPosts.CountAsync());
                }

                Assert.Equal(0, await ImportInitialContentCommand.RunAsync([]));

                await using (var db = new ShopDbContext(options))
                {
                    var about = await db.ContentPages.SingleAsync(p => p.Slug == ContentSlugs.About);
                    var contact = await db.ContentPages.SingleAsync(p => p.Slug == ContentSlugs.Contact);

                    // Still the previous seeded draft -> replaced with the real site text, marker kept.
                    Assert.NotEqual(ContentSeeds.PreviousAboutBody, about.Body);
                    Assert.Contains(ContentPage.DraftMarker, about.Body, StringComparison.Ordinal);

                    // Already edited by the owner -> left alone exactly as it was.
                    Assert.Equal("متنی که مالک فروشگاه خودش نوشته است.", contact.Body);

                    var posts = await db.BlogPosts.OrderBy(p => p.Slug).ToListAsync();
                    Assert.Equal(2, posts.Count);
                    Assert.All(posts, p => Assert.False(p.IsPublished));
                    Assert.Equal("متن آزمایشی A.", posts[0].Body);

                    Assert.Equal(1, await db.ContentAuditLogs.CountAsync());
                    Assert.Equal(2, await db.BlogAuditLogs.CountAsync());
                }

                // Re-run: idempotent (the page was already updated once, the posts already exist by slug).
                Assert.Equal(0, await ImportInitialContentCommand.RunAsync([]));
                await using (var db = new ShopDbContext(options))
                {
                    Assert.Equal(2, await db.BlogPosts.CountAsync());
                    Assert.Equal(1, await db.ContentAuditLogs.CountAsync()); // no second update on re-run
                }
            }
        }
        finally
        {
            Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
            await using var cleanup = new ShopDbContext(options);
            await cleanup.Database.EnsureDeletedAsync();
            Directory.Delete(contentDir, recursive: true);
            if (Directory.Exists(media))
            {
                Directory.Delete(media, recursive: true);
            }
        }
    }

    [Fact]
    public async Task NeverTouchesPaymentFields()
    {
        var connection = ApiFixture.NewDatabaseConnectionString();
        var media = Path.Combine(Path.GetTempPath(), $"shop-content-import-media-{Guid.NewGuid():N}");
        var contentDir = WriteFixtureContent(ManifestTemplate);
        var options = new DbContextOptionsBuilder<ShopDbContext>().UseSqlServer(connection).Options;
        try
        {
            using (new EnvScope(
                ("ASPNETCORE_ENVIRONMENT", "Testing"),
                ("ConnectionStrings__Default", connection),
                ("Database__MigrateOnStartup", "true"),
                ("Media__RootPath", media),
                ("CONTENT_IMPORT_DIR", contentDir),
                ("Seed__AdminEmail", null),
                ("Seed__AdminPassword", null)))
            {
                // The dry run still seeds the first-start defaults (DatabaseInitializer runs before the dry-run check).
                Assert.Equal(0, await ImportInitialContentCommand.RunAsync(["--dry-run"]));

                string cardNumberBefore, cardHolderBefore, bankNameBefore;
                bool cardToCardEnabledBefore;
                await using (var db = new ShopDbContext(options))
                {
                    var setting = await db.StoreSettings.SingleAsync();
                    cardNumberBefore = setting.CardNumber;
                    cardHolderBefore = setting.CardHolderName;
                    bankNameBefore = setting.BankName;
                    cardToCardEnabledBefore = setting.CardToCardEnabled;
                }

                Assert.Equal(0, await ImportInitialContentCommand.RunAsync([]));

                await using (var db = new ShopDbContext(options))
                {
                    var setting = await db.StoreSettings.SingleAsync();
                    Assert.Equal(cardNumberBefore, setting.CardNumber);
                    Assert.Equal(cardHolderBefore, setting.CardHolderName);
                    Assert.Equal(bankNameBefore, setting.BankName);
                    Assert.Equal(cardToCardEnabledBefore, setting.CardToCardEnabled);
                    Assert.Equal("parsagallery2020", setting.InstagramHandle);
                }
            }
        }
        finally
        {
            Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
            await using var cleanup = new ShopDbContext(options);
            await cleanup.Database.EnsureDeletedAsync();
            Directory.Delete(contentDir, recursive: true);
            if (Directory.Exists(media))
            {
                Directory.Delete(media, recursive: true);
            }
        }
    }
}
