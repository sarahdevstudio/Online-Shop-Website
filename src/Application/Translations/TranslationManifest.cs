using System.Reflection;
using System.Text.Json;

namespace Shop.Application.Translations;

/// <summary>
/// The texts shipped with the application: <c>translations/fa-IR.json</c> at the repository root, embedded here. It is the only
/// place default texts live; the seeder copies it into the database and the storefront bundles the same file as its fallback.
/// </summary>
public static class TranslationManifest
{
    public const string ResourceName = "Shop.Application.Translations.fa-IR.json";

    private static readonly Lazy<IReadOnlyDictionary<string, string>> Lazy = new(Load);

    public static IReadOnlyDictionary<string, string> Defaults => Lazy.Value;

    private static IReadOnlyDictionary<string, string> Load()
    {
        using var stream = typeof(TranslationManifest).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource {ResourceName} is missing.");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }
}
