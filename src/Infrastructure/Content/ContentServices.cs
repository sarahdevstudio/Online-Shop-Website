using Microsoft.EntityFrameworkCore;
using Shop.Application.Common;
using Shop.Application.Content;
using Shop.Domain.Content;
using Shop.Infrastructure.Persistence;

namespace Shop.Infrastructure.Content;

internal sealed class ContentService(ShopDbContext db) : IContentService
{
    public async Task<IReadOnlyList<ContentPageSummaryDto>> ListAsync(CancellationToken ct) =>
        await db.ContentPages.AsNoTracking().OrderBy(p => p.Id)
            .Select(p => new ContentPageSummaryDto(p.Slug, p.Title, p.UpdatedAtUtc)).ToListAsync(ct);

    public async Task<Result<ContentPageDto>> GetAsync(string slug, CancellationToken ct)
    {
        var page = await db.ContentPages.AsNoTracking().Where(p => p.Slug == slug)
            .Select(p => new ContentPageDto(p.Slug, p.Title, p.Body, p.UpdatedAtUtc)).SingleOrDefaultAsync(ct);
        return page is null ? Error.NotFound("page_not_found", "Page not found.") : page;
    }
}

internal sealed class AdminContentService(ShopDbContext db, ICurrentUser currentUser, TimeProvider clock) : IAdminContentService
{
    public async Task<IReadOnlyList<AdminContentPageListItemDto>> ListAsync(CancellationToken ct)
    {
        var rows = await (
                from p in db.ContentPages.AsNoTracking()
                join u in db.Users.AsNoTracking() on p.UpdatedByUserId equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby p.Id
                select new { p.Slug, p.Title, p.Body, p.UpdatedAtUtc, Email = u.Email })
            .ToListAsync(ct);
        return rows.Select(r => new AdminContentPageListItemDto(
            r.Slug, r.Title, r.Body.Contains(ContentPage.DraftMarker, StringComparison.Ordinal), r.UpdatedAtUtc, r.Email)).ToList();
    }

    public async Task<Result<AdminContentPageDto>> GetAsync(string slug, CancellationToken ct)
    {
        var row = await LoadAsync(slug, ct);
        return row is null ? NotFound() : row;
    }

    public async Task<Result<AdminContentPageDto>> UpdateAsync(string slug, UpdateContentPageRequest request, CancellationToken ct)
    {
        var errors = ContentPage.Validate(request.Title, request.Body);
        if (errors.Count > 0)
        {
            return Error.Validation(errors);
        }

        var page = await db.ContentPages.SingleOrDefaultAsync(p => p.Slug == slug, ct);
        if (page is null)
        {
            return NotFound();
        }

        // The version the editor loaded: if someone saved meanwhile, nothing is overwritten and the editor gets a 409.
        db.Entry(page).Property(p => p.RowVersion).OriginalValue = request.RowVersion;

        var newTitle = Shop.Domain.Common.PersianText.Normalize(request.Title);
        var newBody = Shop.Domain.Common.PersianText.NormalizeLetters(request.Body);
        var titleChanged = newTitle != page.Title;
        var bodyChanged = newBody != page.Body;
        if (titleChanged || bodyChanged)
        {
            var now = clock.GetUtcNow().UtcDateTime;
            page.Apply(request.Title, request.Body, currentUser.UserId, now);
            db.ContentAuditLogs.Add(ContentAuditLog.Create(slug, currentUser.UserId, titleChanged, bodyChanged, now));
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Error.Conflict("concurrent_update", "این صفحه هم‌زمان توسط شخص دیگری تغییر کرد. صفحه را دوباره بارگذاری کنید.");
            }
        }
        else
        {
            // Nothing changed, but a stale editor should still be told: compare against the stored version.
            var current = await db.ContentPages.AsNoTracking().Where(p => p.Slug == slug).Select(p => p.RowVersion).SingleAsync(ct);
            if (!current.SequenceEqual(request.RowVersion))
            {
                return Error.Conflict("concurrent_update", "این صفحه هم‌زمان توسط شخص دیگری تغییر کرد. صفحه را دوباره بارگذاری کنید.");
            }
        }

        return (await LoadAsync(slug, ct))!;
    }

    public async Task<Result<IReadOnlyList<ContentAuditEntryDto>>> AuditAsync(string slug, CancellationToken ct)
    {
        if (!await db.ContentPages.AnyAsync(p => p.Slug == slug, ct))
        {
            return NotFound();
        }

        var rows = await (
                from a in db.ContentAuditLogs.AsNoTracking()
                where a.Slug == slug
                join u in db.Users.AsNoTracking() on a.AdminUserId equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby a.AtUtc descending, a.Id descending
                select new ContentAuditEntryDto(a.AtUtc, a.AdminUserId, u.Email, a.TitleChanged, a.BodyChanged))
            .Take(30)
            .ToListAsync(ct);
        return rows;
    }

    private async Task<AdminContentPageDto?> LoadAsync(string slug, CancellationToken ct)
    {
        var row = await (
                from p in db.ContentPages.AsNoTracking()
                where p.Slug == slug
                join u in db.Users.AsNoTracking() on p.UpdatedByUserId equals u.Id into users
                from u in users.DefaultIfEmpty()
                select new { p.Slug, p.Title, p.Body, p.UpdatedAtUtc, p.RowVersion, Email = u.Email })
            .SingleOrDefaultAsync(ct);
        return row is null
            ? null
            : new AdminContentPageDto(
                row.Slug, row.Title, row.Body, row.Body.Contains(ContentPage.DraftMarker, StringComparison.Ordinal),
                row.UpdatedAtUtc, row.Email, row.RowVersion);
    }

    private static Error NotFound() => Error.NotFound("page_not_found", "Page not found.");
}
