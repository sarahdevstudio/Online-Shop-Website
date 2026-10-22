using System.Text.RegularExpressions;

namespace Shop.Domain.Catalog;

/// <summary>English URL slugs: lower-case ASCII letters and digits in hyphen-separated groups. Immutable once created.</summary>
public static partial class Slug
{
    public const int MaxLength = 100;

    public static bool IsValid(string? value) =>
        value is { Length: > 0 and <= MaxLength } && Pattern().IsMatch(value);

    public static string Require(string? value, string paramName) =>
        IsValid(value) ? value! : throw new ArgumentException($"'{value}' is not a valid slug.", paramName);

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
