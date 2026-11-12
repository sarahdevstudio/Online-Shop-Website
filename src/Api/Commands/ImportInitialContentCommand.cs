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
/// produced (never the network) and the about/contact text baked into <see cref="ContentSeeds"/>, and applies them
/// to the database. One transaction: any error means nothing is written. Idempotent (a second run changes nothing).
///
/// Overwrite rule, owner edits always win: an existing content page or store setting field is overwritten ONLY when
/// its current value is still exactly the value <see cref="ContentSeeds"/>/<c>StoreDefaultsOptions</c> seeded
/// before this import existed (<see cref="PreviousDefaults"/>); any other current value is left alone and printed
/// as "kept (edited by owner)". Blog posts are create-only by slug: this command never updates or removes one.
/// Never touches payment fields (CardNumber, CardHolderName, BankName, CardToCardEnabled).
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
    /// The exact title/body (for the two content pages this import touches) and the exact empty-string store-setting
    /// fields that were seeded before this import existed. A page or field still equal to these values has never been
    /// opened in the admin and is safe to overwrite; anything else is an owner edit and is left alone.
    /// </summary>
    private static class PreviousDefaults
    {
        public const string AboutTitle = "دربارهٔ ما";
        public const string AboutBody = ContentSeeds.PreviousAboutBody;
        public const string ContactTitle = "تماس با ما";
        public const string ContactBody = ContentSeeds.PreviousContactBody;

        public const string StorePhone = "";
        public const string StoreAddress = "";
        public const string InstagramHandle = "";
    }

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

        var about = await db.ContentPages.FirstOrDefaultAsync(p => p.Slug == ContentSlugs.About);
        var contact = await db.ContentPages.FirstOrDefaultAsync(p => p.Slug == ContentSlugs.Contact);
        var storeSetting = await db.StoreSettings.FirstOrDefaultAsync();
        var existingPostSlugs = await db.BlogPosts.Select(p => p.Slug).ToHashSetAsync();

        var aboutSeed = ContentSeeds.All.First(s => s.Slug == ContentSlugs.About);
        var contactSeed = ContentSeeds.All.First(s => s.Slug == ContentSlugs.Contact);

        // The checked-out line endings (LF vs CRLF) of ContentSeeds.cs's raw string literals are not meaningful
        // here — only the text — so both sides are compared with line endings normalized to LF.
        static string Lf(string value) => value.Replace("\r\n", "\n");

        var willUpdateAbout = about is not null && about.Title == PreviousDefaults.AboutTitle && Lf(about.Body) == Lf(PreviousDefaults.AboutBody);
        var willUpdateContact = contact is not null && contact.Title == PreviousDefaults.ContactTitle && Lf(contact.Body) == Lf(PreviousDefaults.ContactBody);

        Console.WriteLine(about is null
            ? "about: no content page row yet (run the app once to seed it first) — skipped"
            : willUpdateAbout ? "about: update (still the previous seeded draft)" : "about: kept (edited by owner)");
        Console.WriteLine(contact is null
            ? "contact: no content page row yet (run the app once to seed it first) — skipped"
            : willUpdateContact ? "contact: update (still the previous seeded draft)" : "contact: kept (edited by owner)");

        var storeFieldChanges = new List<(string Field, string Old, string New)>();
        if (storeSetting is not null)
        {
            if (storeSetting.StorePhone == PreviousDefaults.StorePhone && storeSetting.StorePhone != "۰۲۱۳۳۹۴۵۳۱۹ / ۰۲۱۳۳۹۴۵۵۱۶")
            {
                storeFieldChanges.Add(("storePhone", storeSetting.StorePhone, "۰۲۱۳۳۹۴۵۳۱۹ / ۰۲۱۳۳۹۴۵۵۱۶"));
            }

            if (storeSetting.StoreAddress == PreviousDefaults.StoreAddress && storeSetting.StoreAddress != "تهران، ابتدای خیابان لاله‌زار نو، پلاک ۴۳۴")
            {
                storeFieldChanges.Add(("storeAddress", storeSetting.StoreAddress, "تهران، ابتدای خیابان لاله‌زار نو، پلاک ۴۳۴"));
            }

            if (storeSetting.InstagramHandle == PreviousDefaults.InstagramHandle && storeSetting.InstagramHandle != "parsagallery2020")
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
            if (about is not null && willUpdateAbout)
            {
                about.Apply(aboutSeed.Title, aboutSeed.Body, null, now);
                db.ContentAuditLogs.Add(ContentAuditLog.Create(ContentSlugs.About, null, titleChanged: true, bodyChanged: true, now));
            }

            if (contact is not null && willUpdateContact)
            {
                contact.Apply(contactSeed.Title, contactSeed.Body, null, now);
                db.ContentAuditLogs.Add(ContentAuditLog.Create(ContentSlugs.Contact, null, titleChanged: true, bodyChanged: true, now));
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

        Console.WriteLine($"Updated {(willUpdateAbout ? 1 : 0) + (willUpdateContact ? 1 : 0)} content page(s), " +
            $"{storeFieldChanges.Count} store setting field(s), created {postsToCreate.Count} blog post(s).");
        return 0;
    }
}
