using System.Security.Cryptography;
using System.Text;

namespace Shop.Application.Orders;

/// <summary>The shared secret between the Next.js server and the API (<c>X-Storefront-Key</c>).</summary>
public static class StorefrontKeyRules
{
    public const string HeaderName = "X-Storefront-Key";
    public const int MinLength = 32;

    /// <summary>Constant-time comparison: both sides are hashed first, so neither content nor length leaks through timing.</summary>
    public static bool Matches(string? expected, string? supplied)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(supplied))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(expected)),
            SHA256.HashData(Encoding.UTF8.GetBytes(supplied)));
    }

    /// <summary>Production needs a real random secret: long enough and not an obvious placeholder.</summary>
    public static bool IsAcceptableForProduction(string? key)
    {
        if (key is null || key.Length < MinLength || key.Distinct().Count() < 8)
        {
            return false;
        }

        var lower = key.ToLowerInvariant();
        return !(lower.StartsWith("dev-", StringComparison.Ordinal)
            || lower.StartsWith("test-", StringComparison.Ordinal)
            || new[] { "placeholder", "change-me", "changeme", "change_me", "your-", "your_", "example", "replace", "secret", "todo" }
                .Any(lower.Contains));
    }
}
