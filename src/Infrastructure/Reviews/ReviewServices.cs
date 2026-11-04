using Microsoft.EntityFrameworkCore;
using Shop.Application.Common;
using Shop.Application.Reviews;
using Shop.Domain.Catalog;
using Shop.Domain.Common;
using Shop.Domain.Orders;
using Shop.Domain.Reviews;
using Shop.Infrastructure.Persistence;

namespace Shop.Infrastructure.Reviews;

/// <summary>Guest reviews (docs/18): submitted pending, shown only when a moderator approves them, one per mobile number per product.</summary>
internal sealed class ReviewService(ShopDbContext db, TimeProvider clock) : IReviewService
{
    public const int PublicPageSize = 50;
    private static readonly Error ProductNotFound = Error.NotFound("product_not_found", "api.error.requested-item-not-found");

    public async Task<Result> SubmitAsync(SubmitReviewRequest request, CancellationToken ct)
    {
        // A bot that filled the hidden field gets the same "thanks" a person would, and nothing is stored.
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            return Result.Success();
        }

        var errors = new Dictionary<string, string[]>();
        var name = PersianText.Normalize(request.Name);
        if (name.Length is < ProductReview.NameMinLength or > ProductReview.NameMaxLength)
        {
            errors["name"] = [Msg.Of("api.validation.review-name", ("min", ProductReview.NameMinLength), ("max", ProductReview.NameMaxLength))];
        }

        if (!IranianMobile.TryNormalize(request.Mobile, out var mobile))
        {
            errors["mobile"] = ["api.validation.order-mobile"];
        }

        if (request.Rating is < 1 or > 5)
        {
            errors["rating"] = ["api.validation.review-rating"];
        }

        var text = PersianText.NormalizeLetters(request.Text);
        if (text.Length is < ProductReview.TextMinLength or > ProductReview.TextMaxLength)
        {
            errors["text"] = [Msg.Of("api.validation.review-text", ("min", ProductReview.TextMinLength), ("max", ProductReview.TextMaxLength))];
        }

        if (errors.Count > 0)
        {
            return Result.Failure(Error.Validation(errors));
        }

        var productId = await db.Products.AsNoTracking().Where(p => p.Slug == request.ProductSlug && p.Status == ProductStatus.Published).Select(p => (int?)p.Id).SingleOrDefaultAsync(ct);
        if (productId is null)
        {
            return Result.Failure(ProductNotFound);
        }

        if (await db.ProductReviews.AnyAsync(r => r.ProductId == productId && r.Mobile == mobile, ct))
        {
            return Result.Failure(Error.Conflict("review_exists", "api.error.review-exists"));
        }

        db.ProductReviews.Add(ProductReview.Create(productId.Value, name, mobile, request.Rating, text, clock.GetUtcNow().UtcDateTime));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex, "UX_ProductReviews_ProductMobile"))
        {
            return Result.Failure(Error.Conflict("review_exists", "api.error.review-exists"));
        }

        return Result.Success();
    }

    public async Task<Result<ReviewSummaryDto>> GetApprovedAsync(string productSlug, CancellationToken ct)
    {
        var productId = await db.Products.AsNoTracking().Where(p => p.Slug == productSlug && p.Status != ProductStatus.Draft).Select(p => (int?)p.Id).SingleOrDefaultAsync(ct);
        if (productId is null)
        {
            return ProductNotFound;
        }

        var approved = db.ProductReviews.AsNoTracking().Where(r => r.ProductId == productId && r.Status == ReviewStatus.Approved);
        var count = await approved.CountAsync(ct);
        if (count == 0)
        {
            return new ReviewSummaryDto(0, 0, []);
        }

        var average = Math.Round(await approved.AverageAsync(r => (double)r.Rating, ct), 1);
        var items = await approved.OrderByDescending(r => r.CreatedAtUtc).ThenByDescending(r => r.Id).Take(PublicPageSize)
            .Select(r => new ReviewDto(r.AuthorName, r.Rating, r.Text, r.CreatedAtUtc)).ToListAsync(ct);
        return new ReviewSummaryDto(count, average, items);
    }
}

internal sealed class AdminReviewService(ShopDbContext db, ICurrentUser currentUser, TimeProvider clock) : IAdminReviewService
{
    private const int PageSize = 30;
    public const string ApprovedAction = "approved";
    public const string RejectedAction = "rejected";

    public async Task<AdminReviewListDto> ListAsync(ReviewStatus? status, int page, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        var query = db.ProductReviews.AsNoTracking().AsQueryable();
        if (status is { } wanted)
        {
            query = query.Where(r => r.Status == wanted);
        }

        var total = await query.CountAsync(ct);
        var counts = await db.ProductReviews.AsNoTracking().GroupBy(r => r.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        var rows = await (
                from r in query
                join p in db.Products.AsNoTracking() on r.ProductId equals p.Id
                join u in db.Users.AsNoTracking() on r.ModeratedByUserId equals u.Id into moderators
                from u in moderators.DefaultIfEmpty()
                orderby r.CreatedAtUtc descending, r.Id descending
                select new AdminReviewDto(r.Id, p.Slug, p.Name, r.AuthorName, r.Mobile, r.Rating, r.Text, r.Status, r.CreatedAtUtc, r.ModeratedAtUtc, u.UserName, r.RowVersion))
            .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct);
        int Of(ReviewStatus s) => counts.FirstOrDefault(c => c.Status == s)?.Count ?? 0;
        return new AdminReviewListDto(rows, total, page, PageSize, Of(ReviewStatus.Pending), Of(ReviewStatus.Approved), Of(ReviewStatus.Rejected));
    }

    public Task<Result<AdminReviewDto>> ApproveAsync(int id, ModerateReviewRequest request, CancellationToken ct) => DecideAsync(id, request, ReviewStatus.Approved, ApprovedAction, ct);

    public Task<Result<AdminReviewDto>> RejectAsync(int id, ModerateReviewRequest request, CancellationToken ct) => DecideAsync(id, request, ReviewStatus.Rejected, RejectedAction, ct);

    private async Task<Result<AdminReviewDto>> DecideAsync(int id, ModerateReviewRequest request, ReviewStatus status, string action, CancellationToken ct)
    {
        var review = await db.ProductReviews.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (review is null)
        {
            return Error.NotFound("review_not_found", "api.error.requested-item-not-found");
        }

        db.Entry(review).Property(r => r.RowVersion).OriginalValue = request.RowVersion;
        var now = clock.GetUtcNow().UtcDateTime;
        if (review.Moderate(status, currentUser.UserId, now))
        {
            db.ReviewModerationLogs.Add(ReviewModerationLog.Create(review.Id, action, currentUser.UserId, now));
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Error.Conflict("concurrent_update", "api.error.review-concurrent-update");
        }

        return (await ListByIdAsync(id, ct))!;
    }

    private async Task<AdminReviewDto?> ListByIdAsync(int id, CancellationToken ct) =>
        await (from r in db.ProductReviews.AsNoTracking().Where(r => r.Id == id)
               join p in db.Products.AsNoTracking() on r.ProductId equals p.Id
               join u in db.Users.AsNoTracking() on r.ModeratedByUserId equals u.Id into moderators
               from u in moderators.DefaultIfEmpty()
               select new AdminReviewDto(r.Id, p.Slug, p.Name, r.AuthorName, r.Mobile, r.Rating, r.Text, r.Status, r.CreatedAtUtc, r.ModeratedAtUtc, u.UserName, r.RowVersion))
            .SingleOrDefaultAsync(ct);

    public async Task<ReviewAuditPageDto> AuditAsync(int page, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        var total = await db.ReviewModerationLogs.CountAsync(ct);
        var items = await (
                from l in db.ReviewModerationLogs.AsNoTracking()
                join r in db.ProductReviews.AsNoTracking() on l.ReviewId equals r.Id
                join p in db.Products.AsNoTracking() on r.ProductId equals p.Id
                join u in db.Users.AsNoTracking() on l.UserId equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby l.AtUtc descending, l.Id descending
                select new ReviewAuditEntryDto(l.Id, l.AtUtc, l.ReviewId, l.Action, u.UserName, p.Name))
            .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct);
        return new ReviewAuditPageDto(items, total);
    }
}
