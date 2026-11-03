using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shop.Application.Catalog;
using Shop.Application.Common;
using Shop.Application.Site;
using Shop.Domain.Site;
using Shop.Infrastructure.Catalog;
using Shop.Infrastructure.Persistence;
using Shop.Application.Translations;
using Shop.Domain.Common;

namespace Shop.Infrastructure.Site;

internal sealed class AdminSiteService(
    ShopDbContext db,
    SiteCompiler compiler,
    ISiteConfigService publicConfig,
    IMediaStorage media,
    IOptions<MediaOptions> mediaOptions,
    ICurrentUser currentUser,
    TimeProvider clock) : IAdminSiteService
{
    private const int AuditPageSize = 30;

    private static readonly Error KindNotFound = Error.NotFound("site_kind_not_found", "api.error.site-kind-not-found");
    private static readonly Error DraftNotFound = Error.NotFound("draft_not_found", "api.error.site-draft-missing");
    private static readonly Error VersionNotFound = Error.NotFound("version_not_found", "api.error.site-version-not-found");
    private static readonly Error Conflict = Error.Conflict("concurrent_update", "api.error.site-concurrent-update");

    private static Error Invalid(string key, string message) =>
        Error.Validation(new Dictionary<string, string[]> { [key] = [message] });

    // ---------------------------------------------------------------- reading

    public async Task<IReadOnlyList<SiteOverviewItemDto>> OverviewAsync(CancellationToken ct)
    {
        var rows = await db.SiteConfigVersions.AsNoTracking()
            .Where(v => v.State != SiteState.Archived)
            .Select(v => new { v.Kind, v.State, v.Version, v.UpdatedAtUtc, v.PublishedAtUtc })
            .ToListAsync(ct);
        return Enum.GetValues<SiteKind>().Select(kind =>
        {
            var draft = rows.FirstOrDefault(r => r.Kind == kind && r.State == SiteState.Draft);
            var published = rows.FirstOrDefault(r => r.Kind == kind && r.State == SiteState.Published);
            return new SiteOverviewItemDto(kind.ToRoute(), draft is not null, draft?.UpdatedAtUtc, published?.Version, published?.PublishedAtUtc);
        }).ToList();
    }

    public async Task<Result<SiteKindDto>> GetAsync(string kind, CancellationToken ct) =>
        SiteNames.TryParseKind(kind, out var parsed) ? await LoadAsync(parsed, ct) : KindNotFound;

    public async Task<Result<SiteVersionDto>> GetVersionAsync(string kind, int version, CancellationToken ct)
    {
        if (!SiteNames.TryParseKind(kind, out var parsed))
        {
            return KindNotFound;
        }

        var row = await db.SiteConfigVersions.AsNoTracking().SingleOrDefaultAsync(v => v.Kind == parsed && v.Version == version, ct);
        if (row is null)
        {
            return VersionNotFound;
        }

        var names = await UserNamesAsync([row.PublishedByUserId], ct);
        return new SiteVersionDto(row.Version!.Value, row.State.ToDb(), ToElement(row.Payload), row.PublishedAtUtc, NameOf(names, row.PublishedByUserId));
    }

    private async Task<SiteKindDto> LoadAsync(SiteKind kind, CancellationToken ct)
    {
        var rows = await db.SiteConfigVersions.AsNoTracking().Where(v => v.Kind == kind).ToListAsync(ct);
        var published = rows.FirstOrDefault(r => r.State == SiteState.Published);
        var draft = rows.FirstOrDefault(r => r.State == SiteState.Draft);
        var history = rows.Where(r => r.Version is not null).OrderByDescending(r => r.Version).Take(SiteConfigVersion.KeptVersions).ToList();

        var names = await UserNamesAsync(rows.SelectMany(r => new[] { r.PublishedByUserId, r.UpdatedByUserId }), ct);

        var mediaIds = new HashSet<int>();
        object? effective = null;
        foreach (var row in new[] { draft, published })
        {
            if (row is not null && SiteJson.TryDeserialize(kind, row.Payload) is { } payload)
            {
                mediaIds.UnionWith(SitePayloadValidator.CollectReferences(payload).MediaIds);
                effective ??= payload;
            }
        }

        var files = await db.SiteMedia.AsNoTracking().Where(m => mediaIds.Contains(m.Id)).ToListAsync(ct);
        var mediaMap = files.ToDictionary(m => m.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), m => new SiteMediaDto(media.GetUrl(m.Path), m.Width, m.Height, m.Purpose));

        SiteReportDto? report = null;
        if (effective is ThemePayload theme && Contrast.IsHex(theme.Primary) && Contrast.IsHex(theme.Secondary) && Contrast.IsHex(theme.Accent)
            && Contrast.IsHex(theme.Background) && Contrast.IsHex(theme.Surface) && Contrast.IsHex(theme.Text)
            && Contrast.IsHex(theme.Success) && Contrast.IsHex(theme.Warning) && Contrast.IsHex(theme.Danger))
        {
            report = new SiteReportDto(Contrast.Check(theme).Select(r => r with { Message = Translate.Resolve(r.Message) }).ToList());
        }

        return new SiteKindDto(
            kind.ToRoute(),
            published is null ? null : new SitePublishedDto(published.Version ?? 0, ToElement(published.Payload), published.PublishedAtUtc ?? published.UpdatedAtUtc, NameOf(names, published.PublishedByUserId)),
            draft is null ? null : new SiteDraftDto(ToElement(draft.Payload), draft.RowVersion, draft.UpdatedAtUtc, NameOf(names, draft.UpdatedByUserId)),
            history.Select(r => new SiteVersionInfoDto(r.Version!.Value, r.State.ToDb(), r.PublishedAtUtc, NameOf(names, r.PublishedByUserId))).ToList(),
            mediaMap,
            report);
    }

    private async Task<Dictionary<Guid, string>> UserNamesAsync(IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var wanted = ids.Where(i => i is not null).Select(i => i!.Value).Distinct().ToList();
        return wanted.Count == 0
            ? []
            : await db.Users.AsNoTracking().Where(u => wanted.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.UserName ?? string.Empty, ct);
    }

    private static string? NameOf(Dictionary<Guid, string> names, Guid? id) => id is { } key && names.TryGetValue(key, out var name) ? name : null;

    private static JsonElement ToElement(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    // ---------------------------------------------------------------- draft

    public async Task<Result<SiteKindDto>> SaveDraftAsync(string kindText, SaveSiteDraftRequest request, CancellationToken ct)
    {
        if (!SiteNames.TryParseKind(kindText, out var kind))
        {
            return KindNotFound;
        }

        var validated = await ValidateJsonAsync(kind, request.Payload, ct);
        if (validated.Error is { } error)
        {
            return error;
        }

        var json = validated.Json!;
        var now = clock.GetUtcNow().UtcDateTime;
        var draft = await db.SiteConfigVersions.SingleOrDefaultAsync(v => v.Kind == kind && v.State == SiteState.Draft, ct);
        string? before;
        bool changed;
        if (draft is null)
        {
            // A client that holds a version for a draft that no longer exists (discarded or published meanwhile) is stale.
            if (request.RowVersion is not null)
            {
                return Conflict;
            }

            before = await db.SiteConfigVersions.AsNoTracking().Where(v => v.Kind == kind && v.State == SiteState.Published).Select(v => v.Payload).SingleOrDefaultAsync(ct);
            db.SiteConfigVersions.Add(SiteConfigVersion.NewDraft(kind, json, currentUser.UserId, now));
            changed = true;
        }
        else
        {
            if (request.RowVersion is null || !draft.RowVersion.SequenceEqual(request.RowVersion))
            {
                return Conflict;
            }

            before = draft.Payload;
            db.Entry(draft).Property(v => v.RowVersion).OriginalValue = request.RowVersion;
            changed = draft.Payload != json;
            if (changed)
            {
                draft.SetPayload(json, currentUser.UserId, now);
            }
        }

        if (changed)
        {
            var summary = draft is null ? "created: " + ChangedFields(kind, before, json) : ChangedFields(kind, before, json);
            db.SiteAuditLogs.Add(SiteAuditLog.Create(kind, SiteAuditAction.Saved, null, summary, currentUser.UserId, now));
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict;
        }
        catch (DbUpdateException) when (draft is null)
        {
            // Two first saves at the same time: the unique index lets one win.
            db.ChangeTracker.Clear();
            if (await db.SiteConfigVersions.AsNoTracking().AnyAsync(v => v.Kind == kind && v.State == SiteState.Draft, ct))
            {
                return Conflict;
            }

            throw;
        }

        return await LoadAsync(kind, ct);
    }

    public async Task<Result<SiteKindDto>> DiscardAsync(string kindText, DiscardSiteDraftRequest request, CancellationToken ct)
    {
        if (!SiteNames.TryParseKind(kindText, out var kind))
        {
            return KindNotFound;
        }

        var draft = await db.SiteConfigVersions.SingleOrDefaultAsync(v => v.Kind == kind && v.State == SiteState.Draft, ct);
        if (draft is null)
        {
            return DraftNotFound;
        }

        if (request.RowVersion is null || !draft.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Conflict;
        }

        db.Entry(draft).Property(v => v.RowVersion).OriginalValue = request.RowVersion;
        db.SiteConfigVersions.Remove(draft);
        db.SiteAuditLogs.Add(SiteAuditLog.Create(kind, SiteAuditAction.Discarded, null, "discarded", currentUser.UserId, clock.GetUtcNow().UtcDateTime));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict;
        }

        return await LoadAsync(kind, ct);
    }

    // ---------------------------------------------------------------- publish and restore

    public async Task<Result<SiteKindDto>> PublishAsync(string kindText, PublishSiteRequest request, CancellationToken ct)
    {
        if (!SiteNames.TryParseKind(kindText, out var kind))
        {
            return KindNotFound;
        }

        var draft = await db.SiteConfigVersions.SingleOrDefaultAsync(v => v.Kind == kind && v.State == SiteState.Draft, ct);
        if (draft is null)
        {
            return DraftNotFound;
        }

        if (request.RowVersion is null || !draft.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Conflict;
        }

        db.Entry(draft).Property(v => v.RowVersion).OriginalValue = request.RowVersion;
        return await PublishDraftAsync(kind, draft, restoredVersion: null, ct);
    }

    public async Task<Result<SiteKindDto>> RestoreAsync(string kindText, int version, RestoreSiteVersionRequest request, CancellationToken ct)
    {
        if (!SiteNames.TryParseKind(kindText, out var kind))
        {
            return KindNotFound;
        }

        var source = await db.SiteConfigVersions.AsNoTracking().SingleOrDefaultAsync(v => v.Kind == kind && v.Version == version, ct);
        if (source is null)
        {
            return VersionNotFound;
        }

        // The old payload must still be valid today (a category or image it points to may be gone).
        var validated = await ValidateJsonAsync(kind, ToElement(source.Payload), ct);
        if (validated.Error is { } error)
        {
            return error;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var draft = await db.SiteConfigVersions.SingleOrDefaultAsync(v => v.Kind == kind && v.State == SiteState.Draft, ct);
        if (draft is null)
        {
            draft = SiteConfigVersion.NewDraft(kind, validated.Json!, currentUser.UserId, now);
            db.SiteConfigVersions.Add(draft);
        }
        else
        {
            draft.SetPayload(validated.Json!, currentUser.UserId, now);
        }

        db.SiteAuditLogs.Add(SiteAuditLog.Create(kind, SiteAuditAction.Restored, version, $"restored version {version}", currentUser.UserId, now));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return Conflict;
        }

        return request.Publish ? await PublishDraftAsync(kind, draft, version, ct) : await LoadAsync(kind, ct);
    }

    private async Task<Result<SiteKindDto>> PublishDraftAsync(SiteKind kind, SiteConfigVersion draft, int? restoredVersion, CancellationToken ct)
    {
        // Validate again at publish time: contrast blocks here (a draft may be saved with a failing pair), and references are re-checked.
        var typed = SiteJson.TryDeserialize(kind, draft.Payload);
        if (typed is null)
        {
            return Invalid("payload", "api.validation.site-payload-structure");
        }

        var references = await LoadReferencesAsync(SitePayloadValidator.CollectReferences(typed), ct);
        var result = SitePayloadValidator.Validate(kind, typed, references);
        if (!result.IsValid)
        {
            return Error.Validation(result.Errors);
        }

        if (result.Payload is ThemePayload theme)
        {
            var failing = Contrast.Check(theme).Where(r => !r.Ok).Select(r => r.Message).ToArray();
            if (failing.Length > 0)
            {
                return Error.Validation(new Dictionary<string, string[]> { ["contrast"] = failing });
            }
        }

        var now = clock.GetUtcNow().UtcDateTime;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var published = await db.SiteConfigVersions.SingleOrDefaultAsync(v => v.Kind == kind && v.State == SiteState.Published, ct);
            var before = published?.Payload;
            if (published is not null)
            {
                // Demote first (and save), then promote: the filtered unique index allows one published row at a time.
                published.Archive();
                await db.SaveChangesAsync(ct);
            }

            var next = (await db.SiteConfigVersions.Where(v => v.Kind == kind).MaxAsync(v => v.Version, ct) ?? 0) + 1;
            draft.Publish(next, currentUser.UserId, now);
            var summary = restoredVersion is { } from ? $"restored version {from}" : ChangedFields(kind, before, draft.Payload);
            db.SiteAuditLogs.Add(SiteAuditLog.Create(kind, SiteAuditAction.Published, next, summary, currentUser.UserId, now));
            await db.SaveChangesAsync(ct);

            // Keep the newest 20 versions; older archived rows go (their images stay: media is never deleted here).
            var stale = await db.SiteConfigVersions
                .Where(v => v.Kind == kind && v.Version != null && v.State == SiteState.Archived)
                .OrderByDescending(v => v.Version)
                .Skip(SiteConfigVersion.KeptVersions - 1)
                .ToListAsync(ct);
            if (stale.Count > 0)
            {
                db.SiteConfigVersions.RemoveRange(stale);
                await db.SaveChangesAsync(ct);
            }

            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return Conflict;
        }

        publicConfig.Invalidate();
        return await LoadAsync(kind, ct);
    }

    // ---------------------------------------------------------------- compiled draft

    public Task<SiteConfigDto> CompiledDraftAsync(CancellationToken ct) => compiler.CompileAsync(useDrafts: true, ct);

    // ---------------------------------------------------------------- audit

    public async Task<Result<SiteAuditPageDto>> AuditAsync(string? kind, int page, CancellationToken ct)
    {
        SiteKind? filter = null;
        if (!string.IsNullOrEmpty(kind))
        {
            if (!SiteNames.TryParseKind(kind, out var parsed))
            {
                return Invalid("kind", "api.validation.site-kind-invalid");
            }

            filter = parsed;
        }

        page = Math.Max(1, page);
        var query = db.SiteAuditLogs.AsNoTracking().Where(a => filter == null || a.Kind == filter);
        var total = await query.CountAsync(ct);
        var rows = await (
                from a in query
                join u in db.Users.AsNoTracking() on a.AdminUserId equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby a.AtUtc descending, a.Id descending
                select new { a.AtUtc, a.Kind, a.Action, a.Version, a.Summary, Username = u.UserName })
            .Skip((page - 1) * AuditPageSize).Take(AuditPageSize)
            .ToListAsync(ct);
        var items = rows.Select(r => new SiteAuditEntryDto(r.AtUtc, r.Kind.ToRoute(), r.Action.ToDb(), r.Version, r.Summary, r.Username)).ToList();
        return new SiteAuditPageDto(items, total, page, Math.Max(1, (int)Math.Ceiling(total / (double)AuditPageSize)));
    }

    // ---------------------------------------------------------------- images

    public async Task<Result<SiteImageDto>> UploadImageAsync(SiteImageUpload upload, CancellationToken ct)
    {
        var purpose = upload.Purpose?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!SiteValues.MediaPurposes.Contains(purpose))
        {
            return Invalid("purpose", "api.validation.site-image-purpose");
        }

        var maxBytes = mediaOptions.Value.MaxImageBytes;
        const string sizeMessage = "api.validation.site-image-size";
        if (upload.Length is <= 0 || upload.Length > maxBytes)
        {
            return Invalid("file", sizeMessage);
        }

        using var buffer = new MemoryStream();
        await upload.Content.CopyToAsync(buffer, ct);
        if (buffer.Length is 0 || buffer.Length > maxBytes)
        {
            return Invalid("file", sizeMessage);
        }

        // Trust the bytes, not the file name or the client's content type; SVG is never accepted (it can carry script).
        var info = ImageProbe.Probe(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
        if (info is null)
        {
            return Invalid("file", "api.validation.image-type");
        }

        if (purpose == "favicon")
        {
            if (info.ContentType != "image/png")
            {
                return Invalid("file", "api.validation.site-favicon-png");
            }

            if (info.Width != info.Height || info.Width < 192)
            {
                return Invalid("file", "api.validation.site-favicon-square");
            }
        }

        var alt = PersianTextNormalize(upload.Alt);
        if (alt.Length > SiteMedia.AltMaxLength)
        {
            return Invalid("alt", Msg.Of("api.validation.alt-max", ("max", SiteMedia.AltMaxLength)));
        }

        buffer.Position = 0;
        var path = await media.SaveAsync(buffer, "site", info.Extension, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var row = SiteMedia.Create(path, info.ContentType, info.Width, info.Height, buffer.Length, purpose, currentUser.UserId, now);
        db.SiteMedia.Add(row);
        db.SiteAuditLogs.Add(SiteAuditLog.Create(SiteKindFor(purpose), SiteAuditAction.Uploaded, null, $"image: {purpose}", currentUser.UserId, now));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await media.DeleteAsync(path, CancellationToken.None);
            throw;
        }

        var warnings = purpose is "hero" or "banner" or "og" && info.Width < 1200 ? new[] { "narrow_image" } : [];
        return new SiteImageDto(row.Id, media.GetUrl(path), info.Width, info.Height, buffer.Length, purpose, warnings);
    }

    private static SiteKind SiteKindFor(string purpose) => purpose switch
    {
        "logo" or "favicon" => SiteKind.Theme,
        "og" => SiteKind.Seo,
        _ => SiteKind.Home,
    };

    private static string PersianTextNormalize(string? value) => Shop.Domain.Common.PersianText.Normalize(value);

    // ---------------------------------------------------------------- shared

    private sealed record Validated(string? Json, Error? Error);

    private async Task<Validated> ValidateJsonAsync(SiteKind kind, JsonElement payload, CancellationToken ct)
    {
        if (payload.ValueKind != JsonValueKind.Object || SiteJson.TryDeserialize(kind, payload) is not { } typed)
        {
            return new Validated(null, Invalid("payload", "api.validation.site-payload-structure"));
        }

        var references = await LoadReferencesAsync(SitePayloadValidator.CollectReferences(typed), ct);
        var result = SitePayloadValidator.Validate(kind, typed, references);
        if (!result.IsValid)
        {
            return new Validated(null, Error.Validation(result.Errors));
        }

        var json = SiteJson.Serialize(result.Payload);
        return json.Length > SiteConfigVersion.PayloadMaxLength
            ? new Validated(null, Invalid("payload", "api.validation.site-payload-too-large"))
            : new Validated(json, null);
    }

    private async Task<SiteReferences> LoadReferencesAsync(SiteReferenceIds ids, CancellationToken ct)
    {
        var media = ids.MediaIds.Count == 0
            ? []
            : await db.SiteMedia.AsNoTracking().Where(m => ids.MediaIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Purpose, ct);
        var categories = ids.CategoryIds.Count == 0
            ? []
            : (await db.Categories.AsNoTracking().Where(c => ids.CategoryIds.Contains(c.Id)).Select(c => c.Id).ToListAsync(ct)).ToHashSet();
        var products = ids.ProductIds.Count == 0
            ? []
            : (await db.Products.AsNoTracking().Where(p => ids.ProductIds.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct)).ToHashSet();
        return new SiteReferences(media, categories, products);
    }

    /// <summary>Names of the top-level fields whose values differ ("primary, font"); never the values themselves.</summary>
    private static string ChangedFields(SiteKind kind, string? before, string after)
    {
        if (string.IsNullOrEmpty(before))
        {
            return "initial";
        }

        try
        {
            using var oldDoc = JsonDocument.Parse(before);
            using var newDoc = JsonDocument.Parse(after);
            var names = new List<string>();
            foreach (var property in newDoc.RootElement.EnumerateObject())
            {
                if (!oldDoc.RootElement.TryGetProperty(property.Name, out var old) || old.GetRawText() != property.Value.GetRawText())
                {
                    names.Add(property.Name);
                }
            }

            return names.Count == 0 ? "no changes" : string.Join(", ", names);
        }
        catch (JsonException)
        {
            return kind.ToRoute();
        }
    }
}
