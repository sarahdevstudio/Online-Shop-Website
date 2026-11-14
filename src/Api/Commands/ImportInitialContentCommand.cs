using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Shop.Domain.Blog;
using Shop.Domain.Content;
using Shop.Domain.Settings;
using Shop.Infrastructure;
using Shop.Infrastructure.Content;
using Shop.Infrastructure.Persistence;
using Shop.Infrastructure.Routing;

namespace Shop.Api.Commands;

/// <summary>
/// <c>dotnet Shop.Api.dll import-initial-content [--dry-run]</c>: reads the manifest <c>tools/content-scrape</c>
/// produced (never the network) and the content-page text baked into <see cref="ContentSeeds"/>, and applies them
/// to the database. One transaction: any error means nothing is written. Idempotent (a second run changes nothing).
///
/// Overwrite rule, owner edits always win: an existing content page or store setting field is overwritten ONLY when
/// its current value is still exactly the value <see cref="ContentSeeds"/>/<c>StoreDefaultsOptions</c> seeded
/// before this import existed (<see cref="PreviousPageDefaults"/> / <see cref="PreviousStoreDefaults"/>); any other
/// current value is left alone and printed as "kept (edited by owner)". Blog posts are create-only by slug: this
/// command never updates or removes one. Never touches payment fields (CardNumber, CardHolderName, BankName,
/// CardToCardEnabled).
/// </summary>
public static class ImportInitialContentCommand
{
    public const string Usage = "Usage: import-initial-content [--dry-run]";

    private sealed record ManifestImage(string File, string Alt);
    private sealed record ManifestPost(string Slug, string Title, string Excerpt, string Body, DateTime PublishedAtUtc, ManifestImage Image);
    private sealed record Manifest(List<ManifestPost> Posts);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// The exact title/body every content page was seeded with before this import existed. A page still exactly
    /// equal to its entry here (title and, line-ending-normalized, body) has never been opened in the admin and is
    /// safe to overwrite; anything else is an owner edit and is left alone.
    /// </summary>
    private static readonly Dictionary<string, (string Title, string Body)> PreviousPageDefaults = new()
    {
        [ContentSlugs.About] = ("دربارهٔ ما", ContentSeeds.PreviousAboutBody),
        [ContentSlugs.Contact] = ("تماس با ما", ContentSeeds.PreviousContactBody),
        [ContentSlugs.Terms] = ("شرایط و قوانین", ContentSeeds.PreviousTermsBody),
        [ContentSlugs.Returns] = ("مرجوعی و بازگشت کالا", ContentSeeds.PreviousReturnsBody),
        [ContentSlugs.Privacy] = ("حریم خصوصی", ContentSeeds.PreviousPrivacyBody),
    };

    /// <summary>The exact empty-string store-setting fields seeded before this import existed.</summary>
    private static class PreviousStoreDefaults
    {
        public const string StorePhone = "";
        public const string StoreAddress = "";
        public const string InstagramHandle = "";
    }

    // The checked-out line endings (LF vs CRLF) of ContentSeeds.cs's raw string literals are not meaningful here —
    // only the text — so both sides are compared with line endings normalized to LF.
    private static string Lf(string value) => value.Replace("\r\n", "\n");

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

        var contentDir = Environment.GetEnvironmentVariable("CONTENT_IMPORT_DIR")
            ?? Path.Combine(AppContext.BaseDirectory, "SeedData", "initial-content");
        var manifestPath = Path.Combine(contentDir, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            Console.Error.WriteLine($"No manifest at {manifestPath}. Run tools/content-scrape/scrape.mjs first.");
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
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var now = clock.GetUtcNow().UtcDateTime;

        var pages = await db.ContentPages.Where(p => PreviousPageDefaults.Keys.Contains(p.Slug)).ToDictionaryAsync(p => p.Slug);
        var storeSetting = await db.StoreSettings.FirstOrDefaultAsync();
        var existingPostSlugs = await db.BlogPosts.Select(p => p.Slug).ToHashSetAsync();

        var seedsBySlug = ContentSeeds.All.ToDictionary(s => s.Slug);
        var pagesToUpdate = new List<string>();
        foreach (var slug in ContentSlugs.All)
        {
            if (!pages.TryGetValue(slug, out var page))
            {
                Console.WriteLine($"{slug}: no content page row yet (run the app once to seed it first) — skipped");
                continue;
            }

            var (previousTitle, previousBody) = PreviousPageDefaults[slug];
            var willUpdate = page.Title == previousTitle && Lf(page.Body) == Lf(previousBody);
            Console.WriteLine(willUpdate ? $"{slug}: update (still the previous seeded draft)" : $"{slug}: kept (edited by owner)");
            if (willUpdate)
            {
                pagesToUpdate.Add(slug);
            }
        }

        var storeFieldChanges = new List<(string Field, string Old, string New)>();
        if (storeSetting is not null)
        {
            if (storeSetting.StorePhone == PreviousStoreDefaults.StorePhone && storeSetting.StorePhone != "۰۲۱۳۳۹۴۵۳۱۹ / ۰۲۱۳۳۹۴۵۵۱۶")
            {
                storeFieldChanges.Add(("storePhone", storeSetting.StorePhone, "۰۲۱۳۳۹۴۵۳۱۹ / ۰۲۱۳۳۹۴۵۵۱۶"));
            }

            if (storeSetting.StoreAddress == PreviousStoreDefaults.StoreAddress && storeSetting.StoreAddress != "تهران، ابتدای خیابان لاله‌زار نو، پلاک ۴۳۴")
            {
                storeFieldChanges.Add(("storeAddress", storeSetting.StoreAddress, "تهران، ابتدای خیابان لاله‌زار نو، پلاک ۴۳۴"));
            }

            if (storeSetting.InstagramHandle == PreviousStoreDefaults.InstagramHandle && storeSetting.InstagramHandle != "parsagallery2020")
            {
                storeFieldChanges.Add(("instagramHandle", storeSetting.InstagramHandle, "parsagallery2020"));
            }

            foreach (var (field, oldValue, newValue) in storeFieldChanges)
            {
                Console.WriteLine($"store setting '{field}': update ('{oldValue}' -> '{newValue}')");
            }

            var untouchedFields = new[] { "storePhone", "storeAddress", "instagramHandle" }.Except(storeFieldChanges.Select(c => c.Field));
            foreach (var field in untouchedFields)
            {
                Console.WriteLine($"store setting '{field}': kept (edited by owner, or already matches)");
            }
        }
        else
        {
            Console.WriteLine("store settings: no row yet (run the app once to seed it first) — skipped");
        }

        var postsToCreate = manifest.Posts.Where(p => !existingPostSlugs.Contains(p.Slug)).OrderBy(p => p.Slug).ToList();
        var postsToSkip = manifest.Posts.Count - postsToCreate.Count;
        Console.WriteLine($"blog posts: {postsToCreate.Count} to create, {postsToSkip} already exist (skipped by slug).");
        foreach (var post in postsToCreate)
        {
            Console.WriteLine($"  create {post.Slug} (published {post.PublishedAtUtc:yyyy-MM-dd})");
        }

        if (dryRun)
        {
            Console.WriteLine("Dry run: nothing written.");
            return 0;
        }

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            foreach (var slug in pagesToUpdate)
            {
                var seed = seedsBySlug[slug];
                pages[slug].Apply(seed.Title, seed.Body, null, now);
                db.ContentAuditLogs.Add(ContentAuditLog.Create(slug, null, titleChanged: true, bodyChanged: true, now));
            }

            if (storeSetting is not null && storeFieldChanges.Count > 0)
            {
                var updated = storeSetting.Values;
                foreach (var (field, _, newValue) in storeFieldChanges)
                {
                    updated = field switch
                    {
                        "storePhone" => updated with { StorePhone = newValue },
                        "storeAddress" => updated with { StoreAddress = newValue },
                        "instagramHandle" => updated with { InstagramHandle = newValue },
                        _ => updated,
                    };
                }

                storeSetting.Apply(updated, null, now);
                foreach (var (field, oldValue, newValue) in storeFieldChanges)
                {
                    db.SettingsAuditLogs.Add(SettingsAuditLog.Create(null, field, oldValue, newValue, now));
                }
            }

            foreach (var manifestPost in postsToCreate)
            {
                var post = BlogPost.Create(manifestPost.Slug, manifestPost.Title, manifestPost.Excerpt, manifestPost.Body, publish: false, userId: null, now);
                db.BlogPosts.Add(post);
                db.BlogAuditLogs.Add(BlogAuditLog.Create(manifestPost.Slug, BlogAuditLog.Created, null, now));
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }

        // Same backfill ImportInitialCatalogCommand runs: new blog posts need a route, and the route table is only
        // seeded once at API startup.
        await scope.ServiceProvider.GetRequiredService<RouteSeeder>().EnsureAsync(CancellationToken.None);

        Console.WriteLine($"Updated {pagesToUpdate.Count} content page(s), " +
            $"{storeFieldChanges.Count} store setting field(s), created {postsToCreate.Count} blog post(s).");
        return 0;
    }
}
