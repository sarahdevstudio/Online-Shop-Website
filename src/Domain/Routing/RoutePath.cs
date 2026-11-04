using System.Text.RegularExpressions;
using Shop.Domain.Common;

namespace Shop.Domain.Routing;

/// <summary>
/// The rules for a public URL path (docs/11 C.2, decision C-2: ASCII only). A valid path is lower-case, starts with "/", has no
/// trailing slash and 1-4 segments of letters/digits joined by single hyphens, at most <see cref="MaxLength"/> characters.
/// Reserved paths are refused by <see cref="Validate"/> too, so one call answers "may an admin take this path".
/// </summary>
public static partial class RoutePath
{
    public const int MaxLength = 200;
    public const int MaxSegments = 4;

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SegmentPattern();

    /// <summary>
    /// The canonical spelling of what was typed or requested: trimmed, ASCII letters lower-cased, one leading "/", trailing
    /// slashes removed. It does not make an invalid path valid (non-ASCII letters, "//", dots stay and fail <see cref="Validate"/>);
    /// null/blank becomes "".
    /// </summary>
    public static string Normalize(string? input)
    {
        var text = (input ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return string.Empty;
        }

        var lowered = string.Create(text.Length, text, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                var c = source[i];
                span[i] = c is >= 'A' and <= 'Z' ? (char)(c + 32) : c;
            }
        });
        lowered = lowered.TrimEnd('/');
        return lowered.Length == 0 ? string.Empty : (lowered[0] == '/' ? lowered : "/" + lowered);
    }

    /// <summary>Null when <paramref name="normalized"/> is a valid, non-reserved path; otherwise the encoded message (key + args).</summary>
    public static string? Validate(string normalized)
    {
        if (normalized.Length == 0)
        {
            return Msg.Of("api.validation.route-path-required");
        }

        if (normalized.Length > MaxLength)
        {
            return Msg.Of("api.validation.route-path-too-long", ("max", MaxLength));
        }

        var segments = normalized[1..].Split('/');
        if (segments.Length > MaxSegments || !segments.All(s => SegmentPattern().IsMatch(s)))
        {
            return Msg.Of("api.validation.route-path-invalid", ("segments", MaxSegments));
        }

        return ReservedPaths.IsReserved(normalized) ? Msg.Of("api.validation.route-path-reserved") : null;
    }

    /// <summary>The shape rules without the reserved check (for looking up an incoming request path).</summary>
    public static bool IsWellFormed(string normalized)
    {
        if (normalized.Length is 0 or > MaxLength || normalized[0] != '/')
        {
            return false;
        }

        var segments = normalized[1..].Split('/');
        return segments.Length <= MaxSegments && segments.All(s => SegmentPattern().IsMatch(s));
    }
}
