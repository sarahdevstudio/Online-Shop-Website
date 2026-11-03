using Microsoft.EntityFrameworkCore;
using Shop.Application.Catalog;
using Shop.Application.Common;
using Shop.Application.Translations;
using Shop.Domain.Common;
using Shop.Domain.Translations;
using Shop.Infrastructure.Persistence;

namespace Shop.Infrastructure.Translations;

public sealed class AdminTranslationService(ShopDbContext db, ICurrentUser currentUser, TranslationCache cache, TimeProvider clock) : IAdminTranslationService
{
    private const int PageSize = 30;

    private static Error NotFound() => Error.NotFound("translation_not_found", "api.error.translation-not-found");

    private static Error Stale() => Error.Conflict("concurrent_update", "api.error.translation-concurrent-update");

    public async Task<PagedList<AdminTranslationDto>> ListAsync(string? q, string? area, bool editedOnly, int page, CancellationToken ct)
    {
        var query = db.Translations.AsNoTracking().Where(t => t.Culture == Translation.DefaultCulture);
        if (!string.IsNullOrWhiteSpace(area))
        {
            var prefix = area.Trim().TrimEnd('.') + ".";
            query = query.Where(t => t.Title.StartsWith(prefix));
        }

        if (editedOnly)
        {
            query = query.Where(t => t.Text != t.DefaultText);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            var persian = PersianText.NormalizeLetters(term);
            query = query.Where(t => t.Title.Contains(term) || t.Text.Contains(persian) || t.Text.Contains(term) || t.DefaultText.Contains(persian));
        }

        var total = await query.CountAsync(ct);
        page = Math.Max(page, 1);
        var rows = await (
                from t in query
                join u in db.Users.AsNoTracking() on t.UpdatedByUserId equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby t.Title
                select new { Row = t, Username = u.UserName })
            .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct);
        return new PagedList<AdminTranslationDto>(rows.Select(r => ToDto(r.Row, r.Username)).ToList(), page, PageSize, total);
    }

    public async Task<IReadOnlyList<TranslationAreaDto>> AreasAsync(CancellationToken ct)
    {
        var rows = await db.Translations.AsNoTracking().Where(t => t.Culture == Translation.DefaultCulture)
            .Select(t => new { t.Title, Edited = t.Text != t.DefaultText }).ToListAsync(ct);
        return rows
            .GroupBy(r => Area(r.Title), StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new TranslationAreaDto(g.Key, g.Count(), g.Count(r => r.Edited)))
            .ToList();
    }

    /// <summary>"shop.cart.empty" → "shop.cart"; "common.save" → "common".</summary>
    private static string Area(string key)
    {
        var parts = key.Split('.');
        return parts.Length >= 3 ? $"{parts[0]}.{parts[1]}" : parts[0];
    }

    public async Task<Result<AdminTranslationDto>> GetAsync(int id, CancellationToken ct)
    {
        var dto = await LoadAsync(id, ct);
        return dto is null ? NotFound() : dto;
    }

    public async Task<Result<AdminTranslationDto>> UpdateAsync(int id, UpdateTranslationRequest request, CancellationToken ct)
    {
        var row = await db.Translations.SingleOrDefaultAsync(t => t.Id == id, ct);
        if (row is null)
        {
            return NotFound();
        }

        var text = PersianText.NormalizeLetters(request.Text);
        if (Validate(row, text) is { } invalid)
        {
            return invalid;
        }

        return await SaveAsync(row, text, request.RowVersion, ct);
    }

    public async Task<Result<AdminTranslationDto>> ResetAsync(int id, ResetTranslationRequest request, CancellationToken ct)
    {
        var row = await db.Translations.SingleOrDefaultAsync(t => t.Id == id, ct);
        return row is null ? NotFound() : await SaveAsync(row, row.DefaultText, request.RowVersion, ct);
    }

    private static Error? Validate(Translation row, string text)
    {
        var errors = new Dictionary<string, string[]>();
        if (text.Length == 0)
        {
            errors["text"] = [Msg.Of("api.validation.translation-required")];
        }
        else if (text.Length > Translation.TextMaxLength)
        {
            errors["text"] = [Msg.Of("api.validation.translation-max-length", ("max", Translation.TextMaxLength))];
        }
        else
        {
            var expected = Translate.PlaceholdersOf(row.DefaultText);
            var actual = Translate.PlaceholdersOf(text);
            if (!expected.SetEquals(actual))
            {
                var list = expected.Count == 0 ? Translate.Get("api.validation.translation-no-placeholders") : string.Join(" ", expected.Order(StringComparer.Ordinal).Select(p => "{" + p + "}"));
                errors["text"] = [Msg.Of("api.validation.translation-placeholders", ("placeholders", list))];
            }
        }

        return errors.Count == 0 ? null : Error.Validation(errors);
    }

    private async Task<Result<AdminTranslationDto>> SaveAsync(Translation row, string newText, byte[] rowVersion, CancellationToken ct)
    {
        // The version the editor loaded: if someone saved meanwhile, nothing is overwritten and the editor gets a 409.
        db.Entry(row).Property(t => t.RowVersion).OriginalValue = rowVersion;
        if (!string.Equals(row.Text, newText, StringComparison.Ordinal))
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var old = row.Text;
            row.Edit(newText, currentUser.UserId, now);
            db.TranslationAuditLogs.Add(TranslationAuditLog.Create(row, old, newText, currentUser.UserId, now));
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Stale();
            }

            await cache.ReloadAsync(ct);
        }
        else
        {
            var current = await db.Translations.AsNoTracking().Where(t => t.Id == row.Id).Select(t => t.RowVersion).SingleAsync(ct);
            if (!current.SequenceEqual(rowVersion))
            {
                return Stale();
            }
        }

        return (await LoadAsync(row.Id, ct))!;
    }

    public async Task<Result<IReadOnlyList<TranslationAuditEntryDto>>> AuditAsync(int id, CancellationToken ct)
    {
        var row = await db.Translations.AsNoTracking().Where(t => t.Id == id).Select(t => new { t.Title, t.Culture }).SingleOrDefaultAsync(ct);
        if (row is null)
        {
            return NotFound();
        }

        var entries = await (
                from a in db.TranslationAuditLogs.AsNoTracking()
                where a.Title == row.Title && a.Culture == row.Culture
                join u in db.Users.AsNoTracking() on a.AdminUserId equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby a.AtUtc descending, a.Id descending
                select new TranslationAuditEntryDto(a.AtUtc, u.UserName, a.OldText, a.NewText))
            .Take(30).ToListAsync(ct);
        return entries;
    }

    private async Task<AdminTranslationDto?> LoadAsync(int id, CancellationToken ct)
    {
        var row = await (
                from t in db.Translations.AsNoTracking()
                where t.Id == id
                join u in db.Users.AsNoTracking() on t.UpdatedByUserId equals u.Id into users
                from u in users.DefaultIfEmpty()
                select new { Row = t, Username = u.UserName })
            .SingleOrDefaultAsync(ct);
        return row is null ? null : ToDto(row.Row, row.Username);
    }

    private static AdminTranslationDto ToDto(Translation t, string? username) => new(
        t.Id, t.Title, t.Culture, t.Text, t.DefaultText, t.IsEdited, TranslationManifest.Defaults.ContainsKey(t.Title),
        Translate.PlaceholdersOf(t.DefaultText).Order(StringComparer.Ordinal).ToList(), t.UpdatedAtUtc, username, t.RowVersion);
}
