using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shop.Application.Translations;
using Shop.Domain.Translations;
using Shop.Infrastructure.Persistence;

namespace Shop.Infrastructure.Translations;

/// <summary>
/// Startup seeding from the shipped manifest: inserts the keys that are missing and refreshes the stored default text. An
/// admin's edit is never overwritten (an edited row keeps its text; an unedited one follows the new default).
/// </summary>
public sealed class TranslationSeeder(ShopDbContext db, TimeProvider clock, ILogger<TranslationSeeder> logger)
{
    public async Task EnsureAsync(CancellationToken ct)
    {
        var culture = Translation.DefaultCulture;
        var existing = await db.Translations.Where(t => t.Culture == culture).ToDictionaryAsync(t => t.Title, StringComparer.Ordinal, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var added = 0;
        var refreshed = 0;
        foreach (var (key, text) in TranslationManifest.Defaults)
        {
            if (existing.TryGetValue(key, out var row))
            {
                if (row.RefreshDefault(text, now))
                {
                    refreshed++;
                }
            }
            else
            {
                db.Translations.Add(Translation.Seed(key, culture, text, now));
                added++;
            }
        }

        if (added == 0 && refreshed == 0)
        {
            return;
        }

        try
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Translations seeded: {Added} added, {Refreshed} default text(s) refreshed", added, refreshed);
        }
        catch (DbUpdateException)
        {
            // Another instance seeded the same keys first (unique index); a retry on the next start fills any gap.
            db.ChangeTracker.Clear();
            logger.LogWarning("Translation seeding collided with another instance; it will complete on the next check");
        }
    }
}
