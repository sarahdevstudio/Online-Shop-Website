using Shop.Application.Common;
using Shop.Domain.Reviews;

namespace Shop.Application.Reviews;

/// <summary>A guest's review. <see cref="Website"/> is a hidden field real visitors never fill: a bot that does is silently ignored.</summary>
public sealed record SubmitReviewRequest(string ProductSlug, string Name, string Mobile, int Rating, string Text, string? Website);

/// <summary>An approved review as the product page shows it: no mobile number, ever.</summary>
public sealed record ReviewDto(string Name, int Rating, string Text, DateTime CreatedAtUtc);

/// <summary><see cref="Average"/> is over the approved reviews only (one decimal); 0 and an empty list when there are none.</summary>
public sealed record ReviewSummaryDto(int Count, double Average, IReadOnlyList<ReviewDto> Items);

public sealed record AdminReviewDto(
    int Id, string ProductSlug, string ProductName, string Name, string Mobile, int Rating, string Text, ReviewStatus Status,
    DateTime CreatedAtUtc, DateTime? ModeratedAtUtc, string? ModeratedBy, byte[] RowVersion);

public sealed record AdminReviewListDto(IReadOnlyList<AdminReviewDto> Items, int Total, int Page, int PageSize, int Pending, int Approved, int Rejected);

/// <summary><see cref="RowVersion"/> is the version the moderator saw: a decision over a newer one is a 409.</summary>
public sealed record ModerateReviewRequest(byte[] RowVersion);

public sealed record ReviewAuditEntryDto(int Id, DateTime AtUtc, int ReviewId, string Action, string? AdminUsername, string ProductName);

public sealed record ReviewAuditPageDto(IReadOnlyList<ReviewAuditEntryDto> Items, int Total);

public interface IReviewService
{
    Task<Result> SubmitAsync(SubmitReviewRequest request, CancellationToken ct);

    Task<Result<ReviewSummaryDto>> GetApprovedAsync(string productSlug, CancellationToken ct);
}

public interface IAdminReviewService
{
    Task<AdminReviewListDto> ListAsync(ReviewStatus? status, int page, CancellationToken ct);

    Task<Result<AdminReviewDto>> ApproveAsync(int id, ModerateReviewRequest request, CancellationToken ct);

    Task<Result<AdminReviewDto>> RejectAsync(int id, ModerateReviewRequest request, CancellationToken ct);

    Task<ReviewAuditPageDto> AuditAsync(int page, CancellationToken ct);
}
