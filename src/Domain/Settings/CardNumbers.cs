namespace Shop.Domain.Settings;

/// <summary>Bank card numbers: 16 digits with a valid Luhn check digit. Persian/Arabic digits, spaces and dashes are accepted.</summary>
public static class CardNumbers
{
    /// <summary>The sample number older configuration used; never accepted as a real card.</summary>
    public const string PlaceholderDigits = "6037000000000000";

    public const string PlaceholderHolder = "نام صاحب کارت (نمونه)";

    /// <summary>Digits only (Latin), or null when the input has anything else or is not 16 digits long.</summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var digits = new System.Text.StringBuilder(16);
        foreach (var c in input.Trim())
        {
            switch (c)
            {
                case >= '0' and <= '9':
                    digits.Append(c);
                    break;
                case >= '۰' and <= '۹':
                    digits.Append((char)('0' + (c - '۰')));
                    break;
                case >= '٠' and <= '٩':
                    digits.Append((char)('0' + (c - '٠')));
                    break;
                case ' ' or '-' or '‌':
                    break;
                default:
                    return null;
            }
        }

        return digits.Length == 16 ? digits.ToString() : null;
    }

    public static bool PassesLuhn(string digits)
    {
        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var d = digits[digits.Length - 1 - i] - '0';
            if (i % 2 == 1)
            {
                d *= 2;
                if (d > 9)
                {
                    d -= 9;
                }
            }

            sum += d;
        }

        return sum % 10 == 0;
    }

    /// <summary>A checksum-valid number that is not the sample (or a run of zeros after the bank prefix).</summary>
    public static bool IsUsable(string? input) =>
        Normalize(input) is { } digits && PassesLuhn(digits) && !IsPlaceholder(digits);

    public static bool IsPlaceholder(string digits) =>
        digits == PlaceholderDigits
        || digits.Skip(4).Take(11).All(c => c == '0') // prefix + only zeros + a check digit: a made-up number
        || digits.Distinct().Count() == 1;

    /// <summary>Audit logs and similar: everything but the last 4 digits is hidden.</summary>
    public static string Mask(string? value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        return digits.Length == 0 ? string.Empty : new string('*', Math.Max(digits.Length - 4, 0)) + digits[^Math.Min(4, digits.Length)..];
    }
}
