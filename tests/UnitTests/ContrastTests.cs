using Shop.Application.Site;
using Shop.Domain.Site;

namespace Shop.UnitTests;

public class ContrastTests
{
    /// <summary>
    /// GOLDEN VECTORS shared with the storefront: storefront/tests/contrast.test.ts must assert exactly these ratios
    /// (foreground, background, ratio rounded to 2 decimals). Computed independently with the WCAG 2.x formula.
    /// </summary>
    public static TheoryData<string, string, double> GoldenVectors => new()
    {
        { "#5b0057", "#ffffff", 13.27 },
        { "#76007c", "#ffffff", 10.19 },
        { "#d8c831", "#5b0057", 7.73 },
        { "#d8c831", "#ffffff", 1.72 },
        { "#000000", "#ffffff", 21.00 },
        { "#777777", "#ffffff", 4.48 },
        { "#767676", "#ffffff", 4.54 },
        { "#1a1a1a", "#f2f2f2", 15.55 },
        { "#b3261e", "#ffffff", 6.54 },
        { "#1b7f3b", "#ffffff", 5.07 },
        { "#8a5a00", "#ffffff", 5.93 },
        { "#111111", "#d8c831", 10.99 },
        { "#5b0057", "#f2f2f2", 11.85 },
    };

    [Theory]
    [MemberData(nameof(GoldenVectors))]
    public void Ratio_MatchesTheGoldenVectors(string foreground, string background, double expected)
    {
        Assert.Equal(expected, Math.Round(Contrast.Ratio(foreground, background), 2, MidpointRounding.AwayFromZero));
        Assert.Equal(Contrast.Ratio(foreground, background), Contrast.Ratio(background, foreground)); // order does not matter
    }

    [Theory]
    [InlineData("#5b0057", "#ffffff")] // dark purple: white text
    [InlineData("#76007c", "#ffffff")]
    [InlineData("#d8c831", "#111111")] // gold: dark text (7.73 on the purple is the other way round)
    [InlineData("#ffffff", "#111111")]
    [InlineData("#000000", "#ffffff")]
    public void OnColor_PicksTheReadableOne(string fill, string expected) => Assert.Equal(expected, Contrast.OnColor(fill));

    private static ThemePayload Seed() => new()
    {
        Primary = "#5b0057", Secondary = "#76007c", Accent = "#d8c831", Background = "#ffffff", Surface = "#f2f2f2", Text = "#1a1a1a",
        Success = "#1b7f3b", Warning = "#8a5a00", Danger = "#b3261e", Font = "vazirmatn", Radius = "sm",
    };

    [Fact]
    public void TheApprovedPalette_PassesEveryPair()
    {
        var results = Contrast.Check(Seed());

        Assert.Equal(11, results.Count);
        Assert.All(results, r => Assert.True(r.Ok, r.Message));
        Assert.Equal(3.0, results.Single(r => r.Id == "focus-background").Min);
    }

    [Fact]
    public void AccentGold_IsAcceptedOnlyAsAFill_NotAsText()
    {
        var gold = Seed() with { Primary = "#d8c831" }; // gold as the primary (text) colour
        var failing = Contrast.Check(gold).Where(r => !r.Ok).Select(r => r.Id).ToList();

        Assert.Contains("primary-background", failing);
        Assert.Contains("focus-background", failing);
        Assert.DoesNotContain("on-accent", failing); // as a fill with dark text it is fine
    }

    [Fact]
    public void FailingPair_NamesThePairAndTheRatio_InPersian()
    {
        var weak = Seed() with { Text = "#aaaaaa" };
        var result = Contrast.Check(weak).Single(r => r.Id == "text-background");

        Assert.False(result.Ok);
        Assert.Equal(4.5, result.Min);
        var message = Shop.Application.Translations.Translate.Resolve(result.Message);
        Assert.Contains("متن روی پس‌زمینه", message);
        Assert.Contains("۲٫۳۲", message); // 2.32 : 1
        Assert.Contains("حداقل ۴٫۵", message);
    }

    [Fact]
    public void Palette_WhereNeitherWhiteNorBlackReaches45_IsRejected()
    {
        // A mid-grey fill reads below 4.5 with both white (4.48) and near-black text.
        var mid = Seed() with { Accent = "#777777" };

        Assert.False(Contrast.Check(mid).Single(r => r.Id == "on-accent").Ok);
    }

    [Theory]
    [InlineData("#5b0057", true)]
    [InlineData("#ABCDEF", true)]
    [InlineData("5b0057", false)]
    [InlineData("#5b005", false)]
    [InlineData("#5b00577", false)]
    [InlineData("#gggggg", false)]
    [InlineData("red", false)]
    [InlineData("rgb(1,2,3)", false)]
    [InlineData(null, false)]
    public void IsHex_AcceptsOnlySixDigitHex(string? value, bool expected) => Assert.Equal(expected, Contrast.IsHex(value));
}
