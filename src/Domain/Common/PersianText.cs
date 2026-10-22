using System.Text;

namespace Shop.Domain.Common;

/// <summary>
/// The single Persian text normalizer. <see cref="Normalize"/> is applied to every name before it is stored;
/// <see cref="SearchKey"/> is applied both to stored search keys and to the user's query, so they always match.
/// </summary>
public static class PersianText
{
    public const char Zwnj = '‌';

    /// <summary>
    /// Arabic ي/ى/ك → Persian ی/ک; trims; collapses whitespace runs to one space; collapses ZWNJ runs to one
    /// ZWNJ and drops ZWNJ next to a space or at either end. Digits are left as typed.
    /// </summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var output = new StringBuilder(value.Length);
        bool pendingSpace = false, pendingZwnj = false;
        foreach (var raw in value)
        {
            var c = MapLetter(raw);
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = true;
            }
            else if (c == Zwnj)
            {
                pendingZwnj = true;
            }
            else
            {
                // A space wins over a ZWNJ next to it; nothing is emitted before the first or after the last character.
                if (output.Length > 0 && pendingSpace)
                {
                    output.Append(' ');
                }
                else if (output.Length > 0 && pendingZwnj)
                {
                    output.Append(Zwnj);
                }

                pendingSpace = pendingZwnj = false;
                output.Append(c);
            }
        }

        return output.ToString();
    }

    /// <summary>
    /// Arabic ي/ى/ك → Persian ی/ک and trim only: for multi-line text (descriptions) whose line breaks must survive.
    /// </summary>
    public static string NormalizeLetters(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : string.Concat(value.Select(MapLetter)).Trim();

    /// <summary>
    /// Matching key: <see cref="Normalize"/>, then Persian/Arabic digits → Latin, ZWNJ → space, lower-case.
    /// "لامپ‌ ۹ وات" and "لامپ 9 وات" produce the same key.
    /// </summary>
    public static string SearchKey(string? value)
    {
        var normalized = Normalize(value);
        var output = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            output.Append(c switch
            {
                >= '۰' and <= '۹' => (char)('0' + (c - '۰')),
                >= '٠' and <= '٩' => (char)('0' + (c - '٠')),
                Zwnj => ' ',
                _ => char.ToLowerInvariant(c),
            });
        }

        return output.ToString();
    }

    private static char MapLetter(char c) => c switch
    {
        'ي' or 'ى' => 'ی', // Arabic yeh / alef maksura → Persian yeh
        'ك' => 'ک', // Arabic kaf → Persian keheh
        _ => c,
    };
}
