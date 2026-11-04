namespace Shop.Domain.Reviews;

public enum ReviewStatus
{
    /// <summary>Submitted by a guest, waiting for a moderator. Not shown anywhere.</summary>
    Pending = 0,

    /// <summary>Shown on the product page and counted in the rating.</summary>
    Approved = 1,

    /// <summary>Refused (or taken down after approval). Not shown; the mobile number stays used, so it cannot be resubmitted.</summary>
    Rejected = 2,
}

/// <summary>
/// A guest's review of a product: name, mobile number (never shown), 1–5 stars and a text. One review per mobile number per product
/// (a unique index). Only <see cref="ReviewStatus.Approved"/> reviews are public.
/// </summary>
public sealed class ProductReview
{
    public const int NameMinLength = 2;
    public const int NameMaxLength = 60;
    public const int TextMinLength = 10;
    public const int TextMaxLength = 2000;

    private ProductReview()
    {
        AuthorName = Mobile = Text = string.Empty;
    }

    public int Id { get; private set; }
    public int ProductId { get; private set; }
    public string AuthorName { get; private set; }

    /// <summary>09xxxxxxxxx; for the moderator and for the one-review rule only.</summary>
    public string Mobile { get; private set; }
    public int Rating { get; private set; }
    public string Text { get; private set; }
    public ReviewStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ModeratedAtUtc { get; private set; }
    public Guid? ModeratedByUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static ProductReview Create(int productId, string authorName, string mobile, int rating, string text, DateTime utcNow)
    {
        if (rating is < 1 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(rating));
        }

        return new ProductReview { ProductId = productId, AuthorName = authorName, Mobile = mobile, Rating = rating, Text = text, Status = ReviewStatus.Pending, CreatedAtUtc = utcNow };
    }

    /// <summary>Moves to <paramref name="status"/> (Approved or Rejected); false when it is already there.</summary>
    public bool Moderate(ReviewStatus status, Guid? userId, DateTime utcNow)
    {
        if (status == Status || status == ReviewStatus.Pending)
        {
            return false;
        }

        Status = status;
        ModeratedAtUtc = utcNow;
        ModeratedByUserId = userId;
        return true;
    }
}

/// <summary>Who approved or rejected which review, and when (the review row keeps only the last decision).</summary>
public sealed class ReviewModerationLog
{
    private ReviewModerationLog()
    {
        Action = string.Empty;
    }

    public int Id { get; private set; }
    public int ReviewId { get; private set; }
    public string Action { get; private set; }
    public Guid? UserId { get; private set; }
    public DateTime AtUtc { get; private set; }

    public static ReviewModerationLog Create(int reviewId, string action, Guid? userId, DateTime utcNow) =>
        new() { ReviewId = reviewId, Action = action, UserId = userId, AtUtc = utcNow };
}
