using Microsoft.EntityFrameworkCore;
using Shop.Application.Blog;
using Shop.Application.Common;
using Shop.Domain.Blog;
using Shop.Domain.Common;
using Shop.Infrastructure.Persistence;

namespace Shop.Infrastructure.Blog;

internal sealed class BlogService(ShopDbContext db) : IBlogService
{
    public async Task<BlogListDto> ListAsync(int page, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        var published = db.BlogPosts.AsNoTracking().Where(p => p.IsPublished);
        var total = await published.CountAsync(ct);
        var items = await published
            .OrderByDescending(p => p.PublishedAtUtc).ThenByDescending(p => p.Id)
            .Skip((page - 1) * BlogPaging.PageSize).Take(BlogPaging.PageSize)
            .Select(p => new BlogPostSummaryDto(p.Slug, p.Title, p.Excerpt, p.PublishedAtUtc!.Value))
            .ToListAsync(ct);
        return new BlogListDto(items, page, BlogPaging.PageSize, total, (total + BlogPaging.PageSize - 1) / BlogPaging.PageSize);
    }

    public async Task<Result<BlogPostDto>> GetAsync(string slug, CancellationToken ct)
    {
        var post = await db.BlogPosts.AsNoTracking().Where(p => p.IsPublished && p.Slug == slug)
            .Select(p => new BlogPostDto(p.Slug, p.Title, p.Excerpt, p.Body, p.PublishedAtUtc!.Value, p.UpdatedAtUtc))
            .SingleOrDefaultAsync(ct);
        return post is null ? Error.NotFound("post_not_found", "مقاله پیدا نشد.") : post;
    }

    public async Task<IReadOnlyList<BlogSitemapEntryDto>> SitemapAsync(CancellationToken ct) =>
        await db.BlogPosts.AsNoTracking().Where(p => p.IsPublished)
            .OrderByDescending(p => p.PublishedAtUtc).ThenByDescending(p => p.Id)
            .Select(p => new BlogSitemapEntryDto(p.Slug, p.UpdatedAtUtc)).ToListAsync(ct);
}

internal sealed class AdminBlogService(ShopDbContext db, ICurrentUser currentUser, TimeProvider clock) : IAdminBlogService
{
    private const string SlugIndex = "UX_BlogPosts_Slug";

    public async Task<IReadOnlyList<AdminBlogListItemDto>> ListAsync(CancellationToken ct) =>
        await (
                from p in db.BlogPosts.AsNoTracking()
                join u in db.Users.AsNoTracking() on p.UpdatedByUserId equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby p.CreatedAtUtc descending, p.Id descending
                select new AdminBlogListItemDto(p.Id, p.Slug, p.Title, p.IsPublished, p.PublishedAtUtc, p.UpdatedAtUtc, u.UserName))
            .ToListAsync(ct);

    public async Task<Result<AdminBlogPostDto>> GetAsync(int id, CancellationToken ct)
    {
        var post = await LoadAsync(id, ct);
        return post is null ? NotFound() : post;
    }

    public async Task<Result<AdminBlogPostDto>> CreateAsync(CreateBlogPostRequest request, CancellationToken ct)
    {
        var errors = BlogPost.Validate(request.Slug, request.Title, request.Excerpt, request.Body);
        if (errors.Count > 0)
        {
            return Error.Validation(errors);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var post = BlogPost.Create(request.Slug, request.Title, request.Excerpt, request.Body, request.Publish, currentUser.UserId, now);
        db.BlogPosts.Add(post);
        db.BlogAuditLogs.Add(BlogAuditLog.Create(post.Slug, BlogAuditLog.Created, currentUser.UserId, now));
        if (post.IsPublished)
        {
            db.BlogAuditLogs.Add(BlogAuditLog.Create(post.Slug, BlogAuditLog.Published, currentUser.UserId, now));
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex, SlugIndex))
        {
            return Error.Conflict("slug_taken", "این نشانی قبلاً برای مقالهٔ دیگری استفاده شده است.");
        }

        return (await LoadAsync(post.Id, ct))!;
    }

    public async Task<Result<AdminBlogPostDto>> UpdateAsync(int id, UpdateBlogPostRequest request, CancellationToken ct)
    {
        var errors = BlogPost.Validate(null, request.Title, request.Excerpt, request.Body);
        if (errors.Count > 0)
        {
            return Error.Validation(errors);
        }

        var post = await db.BlogPosts.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (post is null)
        {
            return NotFound();
        }

        // The version the editor loaded: if someone saved meanwhile, nothing is overwritten and the editor gets a 409.
        db.Entry(post).Property(p => p.RowVersion).OriginalValue = request.RowVersion;

        var contentChanged = PersianText.Normalize(request.Title) != post.Title
            || PersianText.Normalize(request.Excerpt) != post.Excerpt
            || PersianText.NormalizeLetters(request.Body) != post.Body;
        var publishChanged = request.Publish != post.IsPublished;
        if (contentChanged || publishChanged)
        {
            var now = clock.GetUtcNow().UtcDateTime;
            post.Apply(request.Title, request.Excerpt, request.Body, request.Publish, currentUser.UserId, now);
            if (contentChanged)
            {
                db.BlogAuditLogs.Add(BlogAuditLog.Create(post.Slug, BlogAuditLog.Updated, currentUser.UserId, now));
            }

            if (publishChanged)
            {
                db.BlogAuditLogs.Add(BlogAuditLog.Create(
                    post.Slug, request.Publish ? BlogAuditLog.Published : BlogAuditLog.Unpublished, currentUser.UserId, now));
            }

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Conflict();
            }
        }
        else
        {
            // Nothing changed, but a stale editor should still be told: compare against the stored version.
            var current = await db.BlogPosts.AsNoTracking().Where(p => p.Id == id).Select(p => p.RowVersion).SingleAsync(ct);
            if (!current.SequenceEqual(request.RowVersion))
            {
                return Conflict();
            }
        }

        return (await LoadAsync(id, ct))!;
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct)
    {
        var post = await db.BlogPosts.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (post is null)
        {
            return Result.Failure(NotFound());
        }

        db.BlogPosts.Remove(post);
        db.BlogAuditLogs.Add(BlogAuditLog.Create(post.Slug, BlogAuditLog.Deleted, currentUser.UserId, clock.GetUtcNow().UtcDateTime));
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<BlogAuditEntryDto>>> AuditAsync(int id, CancellationToken ct)
    {
        var slug = await db.BlogPosts.AsNoTracking().Where(p => p.Id == id).Select(p => p.Slug).SingleOrDefaultAsync(ct);
        if (slug is null)
        {
            return NotFound();
        }

        IReadOnlyList<BlogAuditEntryDto> rows = await (
                from a in db.BlogAuditLogs.AsNoTracking()
                where a.Slug == slug
                join u in db.Users.AsNoTracking() on a.AdminUserId equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby a.AtUtc descending, a.Id descending
                select new BlogAuditEntryDto(a.AtUtc, a.Action, u.UserName))
            .Take(30)
            .ToListAsync(ct);
        return Result<IReadOnlyList<BlogAuditEntryDto>>.Success(rows);
    }

    private async Task<AdminBlogPostDto?> LoadAsync(int id, CancellationToken ct) =>
        await (
                from p in db.BlogPosts.AsNoTracking()
                where p.Id == id
                join u in db.Users.AsNoTracking() on p.UpdatedByUserId equals u.Id into users
                from u in users.DefaultIfEmpty()
                select new AdminBlogPostDto(
                    p.Id, p.Slug, p.Title, p.Excerpt, p.Body, p.IsPublished, p.PublishedAtUtc, p.UpdatedAtUtc, u.UserName, p.RowVersion))
            .SingleOrDefaultAsync(ct);

    private static Error NotFound() => Error.NotFound("post_not_found", "مقاله پیدا نشد.");

    private static Error Conflict() =>
        Error.Conflict("concurrent_update", "این مقاله هم‌زمان توسط شخص دیگری تغییر کرد. صفحه را دوباره بارگذاری کنید.");
}
