namespace Shop.Domain.Translations;

/// <summary>
/// One user-facing text. <see cref="Title"/> is the key ("shop.cart.empty"), the text is Persian (fa-IR) for now. The seeder
/// inserts missing keys and refreshes <see cref="DefaultText"/>; it never overwrites <see cref="Text"/> once an admin edited it.
/// </summary>
public sealed class Translation
{
    public const int KeyMaxLength = 150;
    public const int CultureMaxLength = 10;
    public const int TextMaxLength = 2000;
    public const string DefaultCulture = "fa-IR";

    private Translation()
    {
        Title = string.Empty;
        Culture = DefaultCulture;
        Text = string.Empty;
        DefaultText = string.Empty;
    }

    public int Id { get; private set; }
    public string Title { get; private set; }
    public string Culture { get; private set; }
    public string Text { get; private set; }
    public string DefaultText { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }

    /// <summary>True once an admin changed the text (it differs from the shipped default).</summary>
    public bool IsEdited => !string.Equals(Text, DefaultText, StringComparison.Ordinal);

    public byte[] RowVersion { get; private set; } = [];

    public static Translation Seed(string title, string culture, string text, DateTime utcNow) => new()
    {
        Title = title,
        Culture = culture,
        Text = text,
        DefaultText = text,
        UpdatedAtUtc = utcNow,
    };

    /// <summary>The seeder's refresh of the shipped text. An unedited row follows the new default; an edited one keeps its text.</summary>
    public bool RefreshDefault(string defaultText, DateTime utcNow)
    {
        if (string.Equals(DefaultText, defaultText, StringComparison.Ordinal))
        {
            return false;
        }

        var followDefault = !IsEdited;
        DefaultText = defaultText;
        if (followDefault)
        {
            Text = defaultText;
            UpdatedAtUtc = utcNow;
        }

        return true;
    }

    public void Edit(string text, Guid? userId, DateTime utcNow)
    {
        Text = text;
        UpdatedByUserId = userId;
        UpdatedAtUtc = utcNow;
    }

    public void ResetToDefault(Guid? userId, DateTime utcNow) => Edit(DefaultText, userId, utcNow);
}

/// <summary>Who changed which text, with the old and new value (texts are not secret).</summary>
public sealed class TranslationAuditLog
{
    private TranslationAuditLog()
    {
        Title = string.Empty;
        Culture = Translation.DefaultCulture;
        OldText = string.Empty;
        NewText = string.Empty;
    }

    public int Id { get; private set; }
    public string Title { get; private set; }
    public string Culture { get; private set; }
    public string OldText { get; private set; }
    public string NewText { get; private set; }
    public Guid? AdminUserId { get; private set; }
    public DateTime AtUtc { get; private set; }

    public static TranslationAuditLog Create(Translation row, string oldText, string newText, Guid? adminUserId, DateTime utcNow) => new()
    {
        Title = row.Title,
        Culture = row.Culture,
        OldText = oldText,
        NewText = newText,
        AdminUserId = adminUserId,
        AtUtc = utcNow,
    };
}
