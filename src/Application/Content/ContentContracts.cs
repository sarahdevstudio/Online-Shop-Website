using Shop.Application.Common;

namespace Shop.Application.Content;

/// <summary>Public: what the storefront renders.</summary>
public sealed record ContentPageDto(string Slug, string Title, string Body, DateTime UpdatedAtUtc);

/// <summary>Public: slug + title + last change (footer links, sitemap).</summary>
public sealed record ContentPageSummaryDto(string Slug, string Title, DateTime UpdatedAtUtc);

public sealed record AdminContentPageListItemDto(string Slug, string Title, bool IsDraft, DateTime UpdatedAtUtc, string? UpdatedByEmail);

public sealed record AdminContentPageDto(
    string Slug, string Title, string Body, bool IsDraft, DateTime UpdatedAtUtc, string? UpdatedByEmail, byte[] RowVersion);

/// <summary><see cref="RowVersion"/> is the version the editor loaded: a stale save is a 409.</summary>
public sealed record UpdateContentPageRequest(string Title, string Body, byte[] RowVersion);

public sealed record ContentAuditEntryDto(DateTime AtUtc, Guid? AdminUserId, string? AdminEmail, bool TitleChanged, bool BodyChanged);

public interface IContentService
{
    Task<IReadOnlyList<ContentPageSummaryDto>> ListAsync(CancellationToken ct);

    Task<Result<ContentPageDto>> GetAsync(string slug, CancellationToken ct);
}

public interface IAdminContentService
{
    Task<IReadOnlyList<AdminContentPageListItemDto>> ListAsync(CancellationToken ct);

    Task<Result<AdminContentPageDto>> GetAsync(string slug, CancellationToken ct);

    Task<Result<AdminContentPageDto>> UpdateAsync(string slug, UpdateContentPageRequest request, CancellationToken ct);

    Task<Result<IReadOnlyList<ContentAuditEntryDto>>> AuditAsync(string slug, CancellationToken ct);
}
