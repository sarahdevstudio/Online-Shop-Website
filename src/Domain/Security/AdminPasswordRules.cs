namespace Shop.Domain.Security;

/// <summary>Password rules for admin accounts (stricter than customers'): create-admin and change-password use them.</summary>
public static class AdminPasswordRules
{
    public const int MinLength = 12;
    public const int MaxLength = 128;

    /// <summary>Null when acceptable, otherwise a short English reason (shown on the console / in the API error).</summary>
    public static string? Weakness(string? password, string? email)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
        {
            return $"The password must be at least {MinLength} characters long.";
        }

        if (password.Length > MaxLength)
        {
            return $"The password must be at most {MaxLength} characters long.";
        }

        if (password.Distinct().Count() < 6)
        {
            return "The password is too repetitive (use at least 6 different characters).";
        }

        var local = (email ?? string.Empty).Split('@')[0];
        if (local.Length >= 3 && password.Contains(local, StringComparison.OrdinalIgnoreCase))
        {
            return "The password must not contain the e-mail name.";
        }

        if (password.All(char.IsAsciiDigit))
        {
            return "The password must not be digits only.";
        }

        return null;
    }
}
