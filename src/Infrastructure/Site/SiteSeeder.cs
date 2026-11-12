using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shop.Application.Catalog;
using Shop.Application.Site;
using Shop.Domain.Site;
using Shop.Infrastructure.Persistence;

namespace Shop.Infrastructure.Site;

/// <summary>
/// First-start site configuration: for every kind that has no row at all, a published version 1 built from
/// <see cref="SiteDefaults"/> (and the embedded logo and favicon for the theme). A kind that has any row is never touched,
/// so an admin's changes survive every restart.
/// </summary>
public sealed class SiteSeeder(ShopDbContext db, IMediaStorage media, TimeProvider clock, ILogger<SiteSeeder> logger)
{
    public async Task EnsureAsync(CancellationToken ct)
    {
        var present = (await db.SiteConfigVersions.AsNoTracking().Select(v => v.Kind).Distinct().ToListAsync(ct)).ToHashSet();
        var missing = Enum.GetValues<SiteKind>().Where(k => !present.Contains(k)).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var written = new List<string>();
        foreach (var kind in missing)
        {
            try
            {
                var payload = await BuildAsync(kind, now, ct);
                db.SiteConfigVersions.Add(SiteConfigVersion.NewPublished(kind, 1, SiteJson.Serialize(payload), null, now));
                await db.SaveChangesAsync(ct);
                written.Add(kind.ToRoute());
            }
            catch (DbUpdateException)
            {
                // Another instance seeded it first (unique index); anything else is a real failure.
                db.ChangeTracker.Clear();
                if (!await db.SiteConfigVersions.AsNoTracking().AnyAsync(v => v.Kind == kind, ct))
                {
                    throw;
                }
            }
        }

        if (written.Count > 0)
        {
            logger.LogInformation("Seeded the default site configuration: {Kinds}", string.Join(", ", written));
        }
    }

    private async Task<object> BuildAsync(SiteKind kind, DateTime now, CancellationToken ct)
    {
        switch (kind)
        {
            case SiteKind.Theme:
                var logo = await SaveAssetAsync("logo.png", "logo", now, ct);
                var favicon = await SaveAssetAsync("favicon-512.png", "favicon", now, ct);
                return SiteDefaults.Theme(logo, favicon);
            case SiteKind.Home:
                var top = await db.Categories.AsNoTracking()
                    .Where(c => c.IsActive && c.ParentId == null)
                    .OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
                    .Select(c => c.Id)
                    .ToListAsync(ct);
                return SiteDefaults.Home(top);
            case SiteKind.Chrome:
                var ids = await db.ContentPages.AsNoTracking()
                    .Where(p => p.Slug == Shop.Domain.Content.ContentSlugs.About || p.Slug == Shop.Domain.Content.ContentSlugs.Contact)
                    .Select(p => new { p.Slug, p.Id })
                    .ToDictionaryAsync(p => p.Slug, p => (int?)p.Id, ct);
                return SiteDefaults.Chrome(
                    ids.TryGetValue(Shop.Domain.Content.ContentSlugs.About, out var aboutId) ? aboutId : null,
                    ids.TryGetValue(Shop.Domain.Content.ContentSlugs.Contact, out var contactId) ? contactId : null);
            default:
                return SiteDefaults.ForKind(kind);
        }
    }

    private async Task<int?> SaveAssetAsync(string name, string purpose, DateTime now, CancellationToken ct)
    {
        var assembly = typeof(SiteSeeder).Assembly;
        var resource = assembly.GetManifestResourceNames().SingleOrDefault(n => n.EndsWith("." + name, StringComparison.Ordinal));
        if (resource is null)
        {
            logger.LogWarning("Site seed asset {Asset} is missing; the theme is seeded without it", name);
            return null;
        }

        using var buffer = new MemoryStream();
        await using (var stream = assembly.GetManifestResourceStream(resource)!)
        {
            await stream.CopyToAsync(buffer, ct);
        }

        var info = ImageProbe.Probe(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
        if (info is null)
        {
            logger.LogWarning("Site seed asset {Asset} is not a readable image; the theme is seeded without it", name);
            return null;
        }

        buffer.Position = 0;
        var path = await media.SaveAsync(buffer, "site", info.Extension, ct);
        var row = SiteMedia.Create(path, info.ContentType, info.Width, info.Height, buffer.Length, purpose, null, now);
        db.SiteMedia.Add(row);
        await db.SaveChangesAsync(ct);
        return row.Id;
    }
}
