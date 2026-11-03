using System.Text;
using System.Text.RegularExpressions;

namespace Shop.Domain.Common;

/// <summary>
/// A user-facing message as the domain and application layers carry it: a translation key plus named arguments, in one string
/// ("api.validation.max-length|max=30"). Nothing below the API boundary produces finished text; the boundary turns the encoded
/// string into text with the Translate service. Keys are lower-case ASCII, dot separated (docs/11 B.2).
/// </summary>
public static partial class Msg
{
    [GeneratedRegex(@"^[a-z][a-z0-9-]*(\.[a-z0-9-]+)+(\|.*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex Encoded();

    /// <summary>The message for <paramref name="key"/> with named arguments, e.g. <c>Msg.Of("api.validation.max-length", ("max", 30))</c>.</summary>
    public static string Of(string key, params (string Name, object? Value)[] args)
    {
        if (args.Length == 0)
        {
            return key;
        }

        var builder = new StringBuilder(key);
        foreach (var (name, value) in args)
        {
            builder.Append('|').Append(name).Append('=').Append(Uri.EscapeDataString(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty));
        }

        return builder.ToString();
    }

    /// <summary>True when the text is an encoded message (a key, optionally with arguments) and not already finished text.</summary>
    public static bool IsEncoded(string? text) => !string.IsNullOrEmpty(text) && Encoded().IsMatch(text);

    public static bool TryParse(string? text, out string key, out IReadOnlyList<(string Name, string Value)> args)
    {
        key = string.Empty;
        args = [];
        if (!IsEncoded(text))
        {
            return false;
        }

        var parts = text!.Split('|');
        key = parts[0];
        var list = new List<(string, string)>();
        foreach (var part in parts.Skip(1))
        {
            var at = part.IndexOf('=');
            if (at > 0)
            {
                list.Add((part[..at], Uri.UnescapeDataString(part[(at + 1)..])));
            }
        }

        args = list;
        return true;
    }
}
