using Shop.Domain.Common;
namespace Shop.Domain.Security;

/// <summary>Password rules for admin accounts (stricter than customers'): create-admin and change-password use them.</summary>
public static class AdminPasswordRules
{
    public const int MinLength = 12;
    public const int MaxLength = 128;

    /// <summary>Null when acceptable, otherwise the message (key + arguments) of the reason, shown on the console and in the API error.</summary>
    public static string? Weakness(string? password, string? username)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength)
        {
            return Msg.Of("api.validation.password-min-length", ("min", MinLength));
        }

        if (password.Length > MaxLength)
        {
            return Msg.Of("api.validation.password-max-length", ("max", MaxLength));
        }

        if (password.Distinct().Count() < 6)
        {
            return "api.validation.password-unique-chars";
        }

        var local = (username ?? string.Empty).Split('@')[0];
        if (local.Length >= 3 && password.Contains(local, StringComparison.OrdinalIgnoreCase))
        {
            return "api.validation.password-contains-username";
        }

        if (password.All(char.IsAsciiDigit))
        {
            return "api.validation.password-digits-only";
        }

        return null;
    }
}
