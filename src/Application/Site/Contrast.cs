using System.Globalization;
using System.Text;
using Shop.Domain.Common;
using Shop.Domain.Site;

namespace Shop.Application.Site;

/// <summary>One checked colour pair of the theme (WCAG 2.2 AA).</summary>
public sealed record ContrastResult(string Id, string Foreground, string Background, double Ratio, double Min, bool Ok, string Message);

/// <summary>
/// The WCAG contrast function. The storefront's <c>lib/contrast.ts</c> must give the same ratios: both are pinned by the same
/// golden vectors (see ContrastTests). Colours are <c>#rrggbb</c>.
/// </summary>
public static class Contrast
{
    public const string OnLight = "#ffffff";
    public const string OnDark = "#111111";
    public const double NormalText = 4.5;
    public const double NonText = 3.0;

    public static bool IsHex(string? value) => value is { Length: 7 } && value[0] == '#' && value.Skip(1).All(Uri.IsHexDigit);

    /// <summary>Relative luminance of an sRGB colour, 0 (black) to 1 (white).</summary>
    public static double Luminance(string hex)
    {
        static double Channel(string hex, int index)
        {
            var value = int.Parse(hex.AsSpan(1 + index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel(hex, 0) + 0.7152 * Channel(hex, 1) + 0.0722 * Channel(hex, 2);
    }

    /// <summary>(L1 + 0.05) / (L2 + 0.05) with L1 the lighter colour; 1 to 21.</summary>
    public static double Ratio(string foreground, string background)
    {
        var a = Luminance(foreground);
        var b = Luminance(background);
        var (light, dark) = a >= b ? (a, b) : (b, a);
        return (light + 0.05) / (dark + 0.05);
    }

    /// <summary>White or near-black, whichever reads better on the given fill.</summary>
    public static string OnColor(string background) =>
        Ratio(OnLight, background) >= Ratio(OnDark, background) ? OnLight : OnDark;

    /// <summary>Every required pair of the palette. The caller must have validated the colours (<see cref="IsHex"/>).</summary>
    public static IReadOnlyList<ContrastResult> Check(ThemePayload theme)
    {
        var results = new List<ContrastResult>();

        void Add(string id, string label, string foreground, string background, double min)
        {
            var ratio = Ratio(foreground, background);
            var rounded = Math.Round(ratio, 2, MidpointRounding.AwayFromZero);
            var ok = rounded >= min;
            var message = Msg.Of(ok ? "api.site.contrast-pass" : "api.site.contrast-fail", ("label", label), ("ratio", Format(ratio)), ("min", Format(min)));
            results.Add(new ContrastResult(id, foreground, background, rounded, min, ok, message));
        }

        Add("text-background", "api.site.contrast.text-background", theme.Text, theme.Background, NormalText);
        Add("text-surface", "api.site.contrast.text-surface", theme.Text, theme.Surface, NormalText);
        Add("primary-background", "api.site.contrast.primary-background", theme.Primary, theme.Background, NormalText);
        Add("primary-surface", "api.site.contrast.primary-surface", theme.Primary, theme.Surface, NormalText);
        Add("on-primary", "api.site.contrast.on-primary", OnColor(theme.Primary), theme.Primary, NormalText);
        Add("on-secondary", "api.site.contrast.on-secondary", OnColor(theme.Secondary), theme.Secondary, NormalText);
        Add("on-accent", "api.site.contrast.on-accent", OnColor(theme.Accent), theme.Accent, NormalText);
        Add("success-background", "api.site.contrast.success-background", theme.Success, theme.Background, NormalText);
        Add("warning-background", "api.site.contrast.warning-background", theme.Warning, theme.Background, NormalText);
        Add("danger-background", "api.site.contrast.danger-background", theme.Danger, theme.Background, NormalText);
        Add("focus-background", "api.site.contrast.focus-background", theme.Primary, theme.Background, NonText);
        return results;
    }

    /// <summary>Persian digits and the Arabic decimal separator, two decimals at most ("۴٫۵").</summary>
    public static string Format(double value)
    {
        var text = Math.Round(value, 2, MidpointRounding.AwayFromZero).ToString("0.##", CultureInfo.InvariantCulture);
        var result = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            result.Append(c switch
            {
                >= '0' and <= '9' => (char)('۰' + (c - '0')),
                '.' => '٫',
                _ => c,
            });
        }

        return result.ToString();
    }
}
