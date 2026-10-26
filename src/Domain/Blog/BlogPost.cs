using SlugRules = Shop.Domain.Catalog.Slug;
using Shop.Domain.Common;

namespace Shop.Domain.Blog;

/// <summary>
/// One blog article: a Persian title and excerpt, a Markdown body (rendered safely by the storefront; raw HTML is never
/// rendered) and an English immutable slug for the URL. Only published posts are public.
/// </summary>
public sealed class BlogPost
{
    public const int TitleMaxLength = 150;
    public const int ExcerptMaxLength = 300;
    public const int BodyMaxLength = 100_000;

    private BlogPost()
    {
        Slug = Title = Excerpt = Body = string.Empty;
    }

    public int Id { get; private set; }
    public string Slug { get; private set; }
    public string Title { get; private set; }
    public string Excerpt { get; private set; }
    public string Body { get; private set; }
    public bool IsPublished { get; private set; }

    /// <summary>When the post first went public; kept when it is unpublished and published again.</summary>
    public DateTime? PublishedAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static BlogPost Create(string slug, string title, string excerpt, string body, bool publish, Guid? userId, DateTime utcNow)
    {
        var post = new BlogPost { Slug = SlugRules.Require(slug, nameof(slug)), CreatedAtUtc = utcNow };
        post.Apply(title, excerpt, body, publish, userId, utcNow);
        return post;
    }

    public void Apply(string title, string excerpt, string body, bool publish, Guid? userId, DateTime utcNow)
    {
        Title = PersianText.Normalize(title);
        Excerpt = PersianText.Normalize(excerpt);
        Body = PersianText.NormalizeLetters(body);
        if (publish && !IsPublished)
        {
            PublishedAtUtc ??= utcNow;
        }

        IsPublished = publish;
        UpdatedAtUtc = utcNow;
        UpdatedByUserId = userId;
    }

    /// <summary>Empty when valid, otherwise field → Persian message. <paramref name="slug"/> is checked only when given (create).</summary>
    public static Dictionary<string, string[]> Validate(string? slug, string? title, string? excerpt, string? body)
    {
        var errors = new Dictionary<string, string[]>();
        if (slug is not null && !SlugRules.IsValid(slug))
        {
            errors["slug"] = [$"نشانی باید فقط شامل حروف کوچک انگلیسی و عدد با خط تیره بین بخش‌ها باشد (حداکثر {SlugRules.MaxLength} نویسه)."];
        }

        var t = PersianText.Normalize(title);
        if (t.Length is < 2 or > TitleMaxLength)
        {
            errors["title"] = [$"عنوان باید بین ۲ تا {TitleMaxLength} نویسه باشد."];
        }

        var e = PersianText.Normalize(excerpt);
        if (e.Length is 0 or > ExcerptMaxLength)
        {
            errors["excerpt"] = [$"خلاصه لازم است و حداکثر {ExcerptMaxLength} نویسه می‌تواند باشد."];
        }

        var b = PersianText.NormalizeLetters(body);
        if (b.Length == 0 || b.Length > BodyMaxLength)
        {
            errors["body"] = [$"متن مقاله لازم است و حداکثر {BodyMaxLength} نویسه می‌تواند باشد."];
        }

        return errors;
    }
}

/// <summary>Who did what to which post and when (the content itself is not copied).</summary>
public sealed class BlogAuditLog
{
    public const string Created = "created";
    public const string Updated = "updated";
    public const string Published = "published";
    public const string Unpublished = "unpublished";
    public const string Deleted = "deleted";

    private BlogAuditLog()
    {
        Slug = Action = string.Empty;
    }

    public int Id { get; private set; }
    public string Slug { get; private set; }
    public string Action { get; private set; }
    public Guid? AdminUserId { get; private set; }
    public DateTime AtUtc { get; private set; }

    public static BlogAuditLog Create(string slug, string action, Guid? adminUserId, DateTime utcNow) => new()
    {
        Slug = slug,
        Action = action,
        AdminUserId = adminUserId,
        AtUtc = utcNow,
    };
}
