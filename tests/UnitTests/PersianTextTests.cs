using Shop.Domain.Common;

namespace Shop.UnitTests;

public class PersianTextTests
{
    private const string Zwnj = "‌";

    [Theory]
    [InlineData("لامپ شمعي", "لامپ شمعی")] // Arabic yeh
    [InlineData("لامپ كم مصرف", "لامپ کم مصرف")] // Arabic kaf
    [InlineData("مصطفى", "مصطفی")] // alef maksura
    public void Normalize_ConvertsArabicLettersToPersian(string input, string expected)
    {
        Assert.Equal(expected, PersianText.Normalize(input));
    }

    [Theory]
    [InlineData("  لامپ   LED \t حبابی \n", "لامپ LED حبابی")]
    [InlineData("لامپ LED", "لامپ LED")] // no-break space
    public void Normalize_TrimsAndCollapsesWhitespace(string input, string expected)
    {
        Assert.Equal(expected, PersianText.Normalize(input));
    }

    [Theory]
    [InlineData("چراغ" + Zwnj + Zwnj + "ها", "چراغ" + Zwnj + "ها")] // run collapsed
    [InlineData("چراغ" + Zwnj + " ها", "چراغ ها")] // next to a space: dropped
    [InlineData("چراغ " + Zwnj + "ها", "چراغ ها")]
    [InlineData(Zwnj + "چراغ" + Zwnj, "چراغ")] // at either end: dropped
    [InlineData(Zwnj + " " + Zwnj, "")]
    public void Normalize_HandlesZwnjConsistently(string input, string expected)
    {
        Assert.Equal(expected, PersianText.Normalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_EmptyInput_ReturnsEmpty(string? input)
    {
        Assert.Equal(string.Empty, PersianText.Normalize(input));
    }

    [Fact]
    public void Normalize_KeepsDigitsAsTyped()
    {
        Assert.Equal("لامپ ۹ وات", PersianText.Normalize("لامپ ۹ وات"));
    }

    [Theory]
    [InlineData("لامپ ۱۲ وات", "لامپ 12 وات")] // Persian digits
    [InlineData("لامپ ١٢ وات", "لامپ 12 وات")] // Arabic-Indic digits
    [InlineData("لامپ LED E27", "لامپ led e27")] // lower-case
    [InlineData("چراغ" + Zwnj + "ها", "چراغ ها")] // ZWNJ → space
    [InlineData("  لامپ  كم‌مصرف  ", "لامپ کم مصرف")]
    public void SearchKey_NormalizesDigitsCaseAndZwnj(string input, string expected)
    {
        Assert.Equal(expected, PersianText.SearchKey(input));
    }

    [Fact]
    public void SearchKey_SpellingVariantsProduceTheSameKey()
    {
        Assert.Equal(PersianText.SearchKey("لامپ كم‌مصرف ۹ وات"), PersianText.SearchKey("لامپ  کم مصرف 9 وات"));
    }

    [Fact]
    public void NormalizeLetters_KeepsLineBreaks()
    {
        Assert.Equal("خط يك\nخط دو".Replace('ي', 'ی').Replace('ك', 'ک'), PersianText.NormalizeLetters("  خط يك\nخط دو  "));
    }
}
