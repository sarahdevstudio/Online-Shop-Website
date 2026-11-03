using System.Text.RegularExpressions;

namespace Shop.Application.Site;

/// <summary>
/// Link rules of the site configuration: an internal path with a single leading slash, or an absolute https URL.
/// http, javascript:, data:, mailto: and protocol-relative targets are refused.
/// </summary>
public static partial class SiteLinks
{
    public const int MaxLength = 300;

    [GeneratedRegex(@"^/[A-Za-z0-9\-._~%/?=&#]*$")]
    private static partial Regex InternalPath();

    public static bool IsValid(string? link)
    {
        if (string.IsNullOrEmpty(link) || link.Length > MaxLength || link.Any(c => c <= ' ' || c == 0x7f))
        {
            return false;
        }

        if (link[0] == '/')
        {
            return !link.StartsWith("//", StringComparison.Ordinal) && InternalPath().IsMatch(link);
        }

        return Uri.TryCreate(link, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && uri.Host.Length > 0
            && string.IsNullOrEmpty(uri.UserInfo);
    }
}

/// <summary>
/// Social profile input: a handle or a URL of the right platform. What comes out is the normalized https URL, or null when
/// the text is not a profile of that platform.
/// </summary>
public static partial class SocialLinkNormalizer
{
    public static readonly IReadOnlyList<string> Platforms = ["telegram", "whatsapp", "aparat", "twitter", "pinterest", "youtube", "linkedin"];

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_]{4,31}$")]
    private static partial Regex TelegramHandle();

    [GeneratedRegex(@"^[A-Za-z0-9_]{1,15}$")]
    private static partial Regex TwitterHandle();

    [GeneratedRegex(@"^[A-Za-z0-9_]{3,30}$")]
    private static partial Regex PinterestHandle();

    [GeneratedRegex(@"^[A-Za-z0-9_.\-]{2,60}$")]
    private static partial Regex AparatHandle();

    [GeneratedRegex(@"^@[A-Za-z0-9_.\-]{3,30}$")]
    private static partial Regex YoutubeHandle();

    [GeneratedRegex(@"^(?:company|in)/[A-Za-z0-9_.\-%]{2,100}$")]
    private static partial Regex LinkedinPath();

    [GeneratedRegex(@"^\+?[0-9]{8,15}$")]
    private static partial Regex WhatsappNumber();

    public static string? Normalize(string platform, string? input)
    {
        var text = input?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > 200)
        {
            return null;
        }

        var (host, path) = SplitUrl(text);
        return platform switch
        {
            "telegram" => FromHandleOrUrl(text, host, path, ["t.me", "telegram.me"], TelegramHandle(), h => $"https://t.me/{h}", stripAt: true),
            "twitter" => FromHandleOrUrl(text, host, path, ["twitter.com", "x.com"], TwitterHandle(), h => $"https://x.com/{h}", stripAt: true),
            "pinterest" => FromHandleOrUrl(text, host, path, ["pinterest.com"], PinterestHandle(), h => $"https://www.pinterest.com/{h}", stripAt: false),
            "aparat" => FromHandleOrUrl(text, host, path, ["aparat.com"], AparatHandle(), h => $"https://www.aparat.com/{h}", stripAt: false),
            "youtube" => Youtube(text, host, path),
            "linkedin" => Linkedin(text, host, path),
            "whatsapp" => Whatsapp(text, host, path),
            _ => null,
        };
    }

    /// <summary>(host without www, first path without leading/trailing slash) of an https URL; (null, null) when it is not one.</summary>
    private static (string? Host, string? Path) SplitUrl(string text)
    {
        if (!text.Contains("://", StringComparison.Ordinal) && !text.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            return (null, null);
        }

        var candidate = text.Contains("://", StringComparison.Ordinal) ? text : "https://" + text;
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return ("invalid", null);
        }

        var host = uri.Host.ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host[4..];
        }

        return (host, Uri.UnescapeDataString(uri.AbsolutePath).Trim('/'));
    }

    private static string? FromHandleOrUrl(string text, string? host, string? path, string[] hosts, Regex handle, Func<string, string> build, bool stripAt)
    {
        if (host is null)
        {
            var bare = stripAt ? text.TrimStart('@') : text;
            return handle.IsMatch(bare) ? build(bare) : null;
        }

        if (!hosts.Contains(host) || path is null || path.Contains('/'))
        {
            return null;
        }

        var name = stripAt ? path.TrimStart('@') : path;
        return handle.IsMatch(name) ? build(name) : null;
    }

    private static string? Youtube(string text, string? host, string? path)
    {
        if (host is null)
        {
            return YoutubeHandle().IsMatch(text) ? $"https://www.youtube.com/{text}" : null;
        }

        if (host is not ("youtube.com" or "m.youtube.com") || path is null)
        {
            return null;
        }

        var parts = path.Split('/');
        var ok = (parts.Length == 1 && YoutubeHandle().IsMatch(parts[0]))
            || (parts.Length == 2 && parts[0] is "channel" or "c" or "user" && Regex.IsMatch(parts[1], @"^[A-Za-z0-9_.\-]{2,60}$"));
        return ok ? $"https://www.youtube.com/{path}" : null;
    }

    private static string? Linkedin(string text, string? host, string? path)
    {
        var candidate = host is null ? text.Trim('/') : host == "linkedin.com" ? path : null;
        return candidate is not null && LinkedinPath().IsMatch(candidate) ? $"https://www.linkedin.com/{candidate}" : null;
    }

    private static string? Whatsapp(string text, string? host, string? path)
    {
        var number = host is null ? text : host == "wa.me" ? path : null;
        if (number is null || !WhatsappNumber().IsMatch(number))
        {
            return null;
        }

        return $"https://wa.me/{number.TrimStart('+')}";
    }
}
