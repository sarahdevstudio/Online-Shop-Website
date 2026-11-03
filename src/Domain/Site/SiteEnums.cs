namespace Shop.Domain.Site;

/// <summary>The independently versioned parts of the site configuration. The route value is the lower-case name.</summary>
public enum SiteKind
{
    Theme,
    Home,
    Chrome,
    Seo,
    Badges,
}

public enum SiteState
{
    Draft,
    Published,
    Archived,
}

public enum SiteAuditAction
{
    Saved,
    Published,
    Restored,
    Discarded,
    Uploaded,
}

/// <summary>Conversions between the enums and the lower-case strings used in URLs and in the database.</summary>
public static class SiteNames
{
    public static string ToRoute(this SiteKind kind) => kind.ToString().ToLowerInvariant();

    public static string ToDb(this SiteState state) => state.ToString().ToLowerInvariant();

    public static string ToDb(this SiteAuditAction action) => action.ToString().ToLowerInvariant();

    public static bool TryParseKind(string? value, out SiteKind kind)
    {
        foreach (var candidate in Enum.GetValues<SiteKind>())
        {
            if (string.Equals(candidate.ToRoute(), value, StringComparison.Ordinal))
            {
                kind = candidate;
                return true;
            }
        }

        kind = default;
        return false;
    }

    public static SiteState ParseState(string value) =>
        Enum.Parse<SiteState>(value, ignoreCase: true);

    public static SiteAuditAction ParseAction(string value) =>
        Enum.Parse<SiteAuditAction>(value, ignoreCase: true);
}
