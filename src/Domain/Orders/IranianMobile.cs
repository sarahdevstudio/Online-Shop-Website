namespace Shop.Domain.Orders;

/// <summary>Mobile numbers are stored as 09xxxxxxxxx; Persian/Arabic digits and +98 / 0098 / 98 / missing-0 forms are accepted.</summary>
public static class IranianMobile
{
    public static bool TryNormalize(string? input, out string mobile)
    {
        mobile = string.Empty;

        // Spaces, dashes, parentheses and a leading + are formatting; anything else is not a phone number.
        var cleaned = new string(Digits.ToLatin(input).Where(c => c is not (' ' or '-' or '+' or '(' or ')')).ToArray());
        if (cleaned.Length == 0 || !cleaned.All(char.IsAsciiDigit))
        {
            return false;
        }

        if (cleaned.StartsWith("0098", StringComparison.Ordinal))
        {
            cleaned = "0" + cleaned[4..];
        }
        else if (cleaned.StartsWith("98", StringComparison.Ordinal) && cleaned.Length == 12)
        {
            cleaned = "0" + cleaned[2..];
        }
        else if (cleaned.StartsWith('9') && cleaned.Length == 10)
        {
            cleaned = "0" + cleaned;
        }

        if (cleaned.Length != 11 || !cleaned.StartsWith("09", StringComparison.Ordinal))
        {
            return false;
        }

        mobile = cleaned;
        return true;
    }
}

public static class Digits
{
    /// <summary>Persian (۰-۹) and Arabic-Indic (٠-٩) digits → Latin; trims; everything else is kept.</summary>
    public static string ToLatin(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return string.Concat(value.Trim().Select(c => c switch
        {
            >= '۰' and <= '۹' => (char)('0' + (c - '۰')),
            >= '٠' and <= '٩' => (char)('0' + (c - '٠')),
            _ => c,
        }));
    }
}
