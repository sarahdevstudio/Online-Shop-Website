namespace Shop.Domain.Seo;

/// <summary>
/// The SEO fields of one product, category, info page or blog post. Empty title / description mean "use the generated default".
/// One row per entity (unique on type + id); rows are created on the first save and removed with the entity.
/// </summary>
public sealed class SeoMeta
{
    public const int TitleMaxLength = 120;
    public const int DescriptionMaxLength = 320;

    private SeoMeta()
    {
        TargetType = string.Empty;
    }

    public int Id { get; private set; }
    public string TargetType { get; private set; }
    public int TargetId { get; private set; }
    public string? MetaTitle { get; private set; }
    public string? MetaDescription { get; private set; }

    /// <summary>A site media id (uploaded through the site image upload); null = the site default share image.</summary>
    public int? OgMediaId { get; private set; }

    /// <summary>The page stays reachable but asks search engines not to index it, and is left out of the sitemaps.</summary>
    public bool NoIndex { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static SeoMeta Create(string targetType, int targetId) => new() { TargetType = targetType, TargetId = targetId };

    public void Apply(string? title, string? description, int? ogMediaId, bool noIndex, Guid? userId, DateTime utcNow)
    {
        MetaTitle = title;
        MetaDescription = description;
        OgMediaId = ogMediaId;
        NoIndex = noIndex;
        UpdatedByUserId = userId;
        UpdatedAtUtc = utcNow;
    }
}

/// <summary>One question and answer of a product's or category's FAQ (plain text; becomes FAQPage structured data).</summary>
public sealed class FaqItem
{
    public const int MaxPerTarget = 20;
    public const int QuestionMaxLength = 200;
    public const int AnswerMaxLength = 2000;

    private FaqItem()
    {
        TargetType = Question = Answer = string.Empty;
    }

    public int Id { get; private set; }
    public string TargetType { get; private set; }
    public int TargetId { get; private set; }
    public string Question { get; private set; }
    public string Answer { get; private set; }
    public int SortOrder { get; private set; }

    public static FaqItem Create(string targetType, int targetId, string question, string answer, int sortOrder) =>
        new() { TargetType = targetType, TargetId = targetId, Question = question, Answer = answer, SortOrder = sortOrder };
}
