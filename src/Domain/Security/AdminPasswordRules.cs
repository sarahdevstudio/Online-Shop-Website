namespace Shop.Domain.Security;

/// <summary>Password rules for admin accounts (stricter than customers'): create-admin and change-password use them.</summary>
public static class AdminPasswordRules
{
    public const int MinLength = 12;
    public const int MaxLength = 128;

    /// <summary>Null when acceptable, otherwise a short Persian reason (shown on the console / in the API error).</summary>
    public static string? Weakness(string? password, string? username)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
        {
            return $"رمز عبور باید حداقل {MinLength} نویسه باشد.";
        }

        if (password.Length > MaxLength)
        {
            return $"رمز عبور باید حداکثر {MaxLength} نویسه باشد.";
        }

        if (password.Distinct().Count() < 6)
        {
            return "رمز عبور بیش از حد تکراری است (دست‌کم ۶ نویسهٔ متفاوت به کار ببرید).";
        }

        var local = (username ?? string.Empty).Split('@')[0];
        if (local.Length >= 3 && password.Contains(local, StringComparison.OrdinalIgnoreCase))
        {
            return "رمز عبور نباید شامل نام کاربری باشد.";
        }

        if (password.All(char.IsAsciiDigit))
        {
            return "رمز عبور نباید فقط عدد باشد.";
        }

        return null;
    }
}
