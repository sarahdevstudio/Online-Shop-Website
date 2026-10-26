using Shop.Domain.Common;

namespace Shop.Domain.Content;

/// <summary>The fixed set of info pages. Admins edit title and body; they can't add, rename or delete pages.</summary>
public static class ContentSlugs
{
    public const string About = "about";
    public const string Contact = "contact";
    public const string Terms = "terms";
    public const string Returns = "returns";
    public const string Privacy = "privacy";

    public static IReadOnlyList<string> All { get; } = [About, Contact, Terms, Returns, Privacy];

    public static bool IsKnown(string? slug) => slug is not null && All.Contains(slug, StringComparer.Ordinal);
}

/// <summary>One info page: a title and a Markdown body (rendered safely by the storefront; raw HTML is never rendered).</summary>
public sealed class ContentPage
{
    public const int TitleMaxLength = 100;
    public const int BodyMaxLength = 50_000;

    /// <summary>Seeded drafts start with this marker; a page whose body still contains it is shown as "draft" in the admin.</summary>
    public const string DraftMarker = "[پیش‌نویس — بازبینی شود]";

    private ContentPage()
    {
        Slug = Title = Body = string.Empty;
    }

    public int Id { get; private set; }
    public string Slug { get; private set; }
    public string Title { get; private set; }
    public string Body { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsDraft => Body.Contains(DraftMarker, StringComparison.Ordinal);

    public static ContentPage Create(string slug, string title, string body, DateTime utcNow)
    {
        if (!ContentSlugs.IsKnown(slug))
        {
            throw new ArgumentException($"'{slug}' is not one of the fixed content pages.", nameof(slug));
        }

        var page = new ContentPage { Slug = slug };
        page.Apply(title, body, null, utcNow);
        return page;
    }

    public void Apply(string title, string body, Guid? userId, DateTime utcNow)
    {
        Title = PersianText.Normalize(title);
        Body = PersianText.NormalizeLetters(body);
        UpdatedAtUtc = utcNow;
        UpdatedByUserId = userId;
    }

    /// <summary>Null when valid, otherwise field → message (Persian, shown in the admin form).</summary>
    public static Dictionary<string, string[]> Validate(string? title, string? body)
    {
        var errors = new Dictionary<string, string[]>();
        var t = PersianText.Normalize(title);
        if (t.Length is < 2 or > TitleMaxLength)
        {
            errors["title"] = [$"عنوان باید بین ۲ تا {TitleMaxLength} نویسه باشد."];
        }

        var b = PersianText.NormalizeLetters(body);
        if (b.Length == 0 || b.Length > BodyMaxLength)
        {
            errors["body"] = [$"متن صفحه لازم است و حداکثر {BodyMaxLength} نویسه می‌تواند باشد."];
        }

        return errors;
    }
}

/// <summary>Who changed which page and when (the content itself is not copied).</summary>
public sealed class ContentAuditLog
{
    private ContentAuditLog()
    {
        Slug = string.Empty;
    }

    public int Id { get; private set; }
    public string Slug { get; private set; }
    public Guid? AdminUserId { get; private set; }
    public DateTime AtUtc { get; private set; }
    public bool TitleChanged { get; private set; }
    public bool BodyChanged { get; private set; }

    public static ContentAuditLog Create(string slug, Guid? adminUserId, bool titleChanged, bool bodyChanged, DateTime utcNow) => new()
    {
        Slug = slug,
        AdminUserId = adminUserId,
        AtUtc = utcNow,
        TitleChanged = titleChanged,
        BodyChanged = bodyChanged,
    };
}
