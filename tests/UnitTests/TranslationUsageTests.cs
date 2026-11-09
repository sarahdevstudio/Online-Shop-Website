using System.Text.RegularExpressions;
using Shop.Application.Translations;

namespace Shop.UnitTests;

/// <summary>
/// Keeps the code and the shipped texts in step (docs/11 B.3, B.4): every key the C# code names exists in the manifest, no
/// user-facing Persian text is hard-coded outside the few documented places, and every text's placeholders are well formed.
/// </summary>
public class TranslationUsageTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "translations", "fa-IR.json")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private static IEnumerable<string> SourceFiles() =>
        Directory.EnumerateFiles(Path.Combine(Root(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && !f.Contains("Migrations"));

    [Fact]
    public void EveryKeyNamedInCode_IsInTheManifest()
    {
        var key = new Regex("\"((?:api|common|shop|admin|seo)\\.[a-z0-9]+(?:[-.][a-z0-9]+)*)(?:\\|[^\"]*)?\"", RegexOptions.CultureInvariant);
        var missing = new List<string>();
        foreach (var file in SourceFiles())
        {
            var code = string.Join('\n', File.ReadAllLines(file).Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));
            foreach (Match match in key.Matches(code))
            {
                if (!TranslationManifest.Defaults.ContainsKey(match.Groups[1].Value))
                {
                    missing.Add($"{Path.GetFileName(file)}: {match.Groups[1].Value}");
                }
            }
        }

        Assert.True(missing.Count == 0, "Keys used in code but missing from translations/fa-IR.json:\n" + string.Join('\n', missing.Distinct()));
    }

    /// <summary>Documented exceptions: seed/sample data (not translations, docs/11 B.7), the generic last-resort text and fixed markers.</summary>
    private static readonly string[] AllowedFiles =
    [
        "CatalogSeeder.cs", "ContentSeeds.cs", "SiteDefaults.cs", "ImportInitialCatalogCommand.cs", // data seeds
        "Translate.cs",   // the generic text when even the manifest is unreachable
        "ContentPage.cs", // DraftMarker: a marker stored inside seeded page bodies
        "CardNumbers.cs", // PlaceholderHolder: the sample card holder that is refused as real data
    ];

    [Fact]
    public void NoUserFacingPersianTextIsHardCodedInCSharp()
    {
        var literal = new Regex("\\$?\"(?:[^\"\\\\]|\\\\.)*[\\u0600-\\u06FF](?:[^\"\\\\]|\\\\.)*\"", RegexOptions.CultureInvariant);
        var found = new List<string>();
        foreach (var file in SourceFiles().Where(f => !AllowedFiles.Contains(Path.GetFileName(f))))
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimStart();
                if (line.StartsWith("//", StringComparison.Ordinal) || line.StartsWith("/*", StringComparison.Ordinal) || line.StartsWith('*'))
                {
                    continue;
                }

                if (literal.IsMatch(lines[i]))
                {
                    found.Add($"{Path.GetFileName(file)}:{i + 1}: {line}");
                }
            }
        }

        Assert.True(found.Count == 0, "Persian text literals outside the translation manifest:\n" + string.Join('\n', found));
    }

    [Fact]
    public void ManifestKeysFollowTheConvention_AndPlaceholdersAreNamedAndAscii()
    {
        var keyShape = new Regex("^(api|common|shop|admin|seo)(\\.[a-z0-9]+(-[a-z0-9]+)*)+$", RegexOptions.CultureInvariant);
        var braces = new Regex("\\{([^{}]*)\\}", RegexOptions.CultureInvariant);
        foreach (var (key, text) in TranslationManifest.Defaults)
        {
            Assert.Matches(keyShape, key);
            Assert.InRange(key.Length, 3, 150);
            Assert.InRange(text.Length, 1, 2000);
            foreach (Match match in braces.Matches(text))
            {
                Assert.Matches("^[A-Za-z][A-Za-z0-9]*$", match.Groups[1].Value);
            }
        }
    }
}
