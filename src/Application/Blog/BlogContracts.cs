using Shop.Application.Common;

namespace Shop.Application.Blog;

public static class BlogPaging
{
    public const int PageSize = 12;
}

/// <summary>Public: one card in the blog list.</summary>
public sealed record BlogPostSummaryDto(string Slug, string Title, string Excerpt, DateTime PublishedAtUtc);

/// <summary>Public: one page of the blog list (newest first). A page past the end has no items.</summary>
public sealed record BlogListDto(IReadOnlyList<BlogPostSummaryDto> Items, int Page, int PageSize, int Total, int TotalPages);

/// <summary>Public: a published post.</summary>
public sealed record BlogPostDto(string Slug, string Title, string Excerpt, string Body, DateTime PublishedAtUtc, DateTime UpdatedAtUtc);

/// <summary>Public: slug + last change, for the sitemap.</summary>
public sealed record BlogSitemapEntryDto(string Slug, DateTime UpdatedAtUtc);

public sealed record AdminBlogListItemDto(
    int Id, string Slug, string Title, bool IsPublished, DateTime? PublishedAtUtc, DateTime UpdatedAtUtc, string? UpdatedByUsername);

public sealed record AdminBlogPostDto(
    int Id, string Slug, string Title, string Excerpt, string Body, bool IsPublished, DateTime? PublishedAtUtc, DateTime UpdatedAtUtc,
    string? UpdatedByUsername, byte[] RowVersion);

public sealed record CreateBlogPostRequest(string Slug, string Title, string Excerpt, string Body, bool Publish);

/// <summary><see cref="RowVersion"/> is the version the editor loaded: a stale save is a 409. The slug never changes.</summary>
public sealed record UpdateBlogPostRequest(string Title, string Excerpt, string Body, bool Publish, byte[] RowVersion);

public sealed record BlogAuditEntryDto(DateTime AtUtc, string Action, string? AdminUsername);

public interface IBlogService
{
    Task<BlogListDto> ListAsync(int page, CancellationToken ct);

    Task<Result<BlogPostDto>> GetAsync(string slug, CancellationToken ct);

    Task<IReadOnlyList<BlogSitemapEntryDto>> SitemapAsync(CancellationToken ct);
}

public interface IAdminBlogService
{
    Task<IReadOnlyList<AdminBlogListItemDto>> ListAsync(CancellationToken ct);

    Task<Result<AdminBlogPostDto>> GetAsync(int id, CancellationToken ct);

    Task<Result<AdminBlogPostDto>> CreateAsync(CreateBlogPostRequest request, CancellationToken ct);

    Task<Result<AdminBlogPostDto>> UpdateAsync(int id, UpdateBlogPostRequest request, CancellationToken ct);

    Task<Result> DeleteAsync(int id, CancellationToken ct);

    Task<Result<IReadOnlyList<BlogAuditEntryDto>>> AuditAsync(int id, CancellationToken ct);
}
