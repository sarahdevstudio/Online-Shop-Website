using Shop.Domain.Orders;
using Shop.Domain.Settings;

namespace Shop.UnitTests;

public class StoreSettingsTests
{
    private const string GoodCard = "6037991234567893"; // passes Luhn

    private static StoreSettingsValues Good(Func<StoreSettingsValues, StoreSettingsValues>? change = null)
    {
        var values = new StoreSettingsValues(
            50_000, null, true, true, GoodCard, "رضا احمدی", "ملی", 10, "02112345678", "تهران، خیابان آزادی", "۹ تا ۱۸", "myshop");
        return change is null ? values : change(values);
    }

    // ---- card numbers ----

    [Theory]
    [InlineData("6037991234567893", "6037991234567893")]
    [InlineData("۶۰۳۷۹۹۱۲۳۴۵۶۷۸۹۳", "6037991234567893")]
    [InlineData("٦٠٣٧٩٩١٢٣٤٥٦٧٨٩٣", "6037991234567893")]
    [InlineData("6037 9912 3456 7893", "6037991234567893")]
    [InlineData("6037-9912-3456-7893", "6037991234567893")]
    [InlineData(" 6037991234567893 ", "6037991234567893")]
    public void CardNumber_IsNormalized(string input, string expected) => Assert.Equal(expected, CardNumbers.Normalize(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("603799123456789")] // 15 digits
    [InlineData("60379912345678931")] // 17 digits
    [InlineData("6037x91234567893")]
    public void CardNumber_RejectsOtherInput(string? input) => Assert.Null(CardNumbers.Normalize(input));

    [Theory]
    [InlineData("6037991234567893", true)]
    [InlineData("6219861234567898", true)]
    [InlineData("4111111111111111", true)] // well-known Luhn-valid test number
    [InlineData("6037991234567890", false)]
    [InlineData("6037991234567894", false)]
    [InlineData("1234567812345678", false)]
    public void Luhn_AcceptsOnlyValidCheckDigits(string digits, bool valid) => Assert.Equal(valid, CardNumbers.PassesLuhn(digits));

    [Theory]
    [InlineData("6037991234567893", true)]
    [InlineData("6037000000000004", false)] // Luhn-valid but a run of zeros: still a placeholder
    [InlineData("6037000000000000", false)] // the old sample
    [InlineData("0000000000000000", false)]
    [InlineData("6037991234567890", false)]
    [InlineData("", false)]
    public void IsUsable_RequiresARealLookingValidNumber(string input, bool usable) => Assert.Equal(usable, CardNumbers.IsUsable(input));

    [Theory]
    [InlineData("6037991234567893", "************7893")]
    [InlineData("۶۰۳۷", "")] // not digits we mask: nothing to leak
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Mask_ShowsOnlyTheLastFourDigits(string? input, string expected) => Assert.Equal(expected, CardNumbers.Mask(input));

    // ---- validation ----

    [Fact]
    public void ValidSettings_PassAndAreNormalized()
    {
        var (values, errors) = StoreSettingsRules.Validate(Good(v => v with
        {
            CardNumber = "۶۰۳۷ ۹۹۱۲ ۳۴۵۶ ۷۸۹۳",
            CardHolderName = "  رضا   احمدي ",
            StorePhone = "۰۲۱-۱۲۳۴۵۶۷۸",
            InstagramHandle = "@My.Shop_1",
        }));

        Assert.Empty(errors);
        Assert.Equal(GoodCard, values!.CardNumber);
        Assert.Equal("رضا احمدی", values.CardHolderName); // Arabic yeh → Persian, spaces collapsed
        Assert.Equal("021-12345678", values.StorePhone);
        Assert.Equal("My.Shop_1", values.InstagramHandle);
    }

    [Theory]
    [InlineData(-1L, "shippingFlatFee")]
    [InlineData(StoreSettingsRules.MaxAmount + 1, "shippingFlatFee")]
    public void ShippingFee_MustBeAWholeNonNegativeAmount(long fee, string field)
    {
        var (values, errors) = StoreSettingsRules.Validate(Good(v => v with { ShippingFlatFee = fee }));

        Assert.Null(values);
        Assert.Contains(field, errors.Keys);
    }

    [Fact]
    public void ShippingFee_Zero_AndNullOrZeroThreshold_AreAllowed()
    {
        Assert.Empty(StoreSettingsRules.Validate(Good(v => v with { ShippingFlatFee = 0, FreeShippingThreshold = null })).Errors);
        Assert.Empty(StoreSettingsRules.Validate(Good(v => v with { FreeShippingThreshold = 0 })).Errors);
        Assert.Contains("freeShippingThreshold", StoreSettingsRules.Validate(Good(v => v with { FreeShippingThreshold = -5 })).Errors.Keys);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void MaxQuantityPerLine_MustBeBetween1And100(int max) =>
        Assert.Contains("maxQuantityPerLine", StoreSettingsRules.Validate(Good(v => v with { MaxQuantityPerLine = max })).Errors.Keys);

    [Theory]
    [InlineData("6037991234567890")] // bad check digit
    [InlineData("603799123456789")] // too short
    [InlineData("abcd")]
    [InlineData("6037000000000004")] // zeros after the prefix: placeholder
    public void BadCardNumber_IsRejected_EvenWhenCardToCardIsOff(string card)
    {
        var (values, errors) = StoreSettingsRules.Validate(Good(v => v with { CardToCardEnabled = false, CardNumber = card }));

        Assert.Null(values);
        Assert.Contains("cardNumber", errors.Keys);
    }

    [Fact]
    public void CardToCard_CannotBeEnabled_WithoutACardNumberOrHolder_OrWithPlaceholders()
    {
        Assert.Contains("cardNumber", StoreSettingsRules.Validate(Good(v => v with { CardNumber = "" })).Errors.Keys);
        Assert.Contains("cardHolderName", StoreSettingsRules.Validate(Good(v => v with { CardHolderName = "" })).Errors.Keys);
        Assert.Contains("cardHolderName", StoreSettingsRules.Validate(Good(v => v with { CardHolderName = CardNumbers.PlaceholderHolder })).Errors.Keys);
        Assert.Contains("cardNumber", StoreSettingsRules.Validate(Good(v => v with { CardNumber = CardNumbers.PlaceholderDigits })).Errors.Keys);
    }

    [Fact]
    public void CardToCard_Off_NeedsNoCardDetails()
    {
        var (values, errors) = StoreSettingsRules.Validate(Good(v => v with { CardToCardEnabled = false, CardNumber = "", CardHolderName = "", BankName = "" }));

        Assert.Empty(errors);
        Assert.NotNull(values);
    }

    [Fact]
    public void AtLeastOnePaymentMethod_MustBeEnabled()
    {
        var (values, errors) = StoreSettingsRules.Validate(Good(v => v with { CardToCardEnabled = false, CashOnDeliveryEnabled = false }));

        Assert.Null(values);
        Assert.Contains("paymentMethods", errors.Keys);
        Assert.Empty(StoreSettingsRules.Validate(Good(v => v with { CardToCardEnabled = false })).Errors);
        Assert.Empty(StoreSettingsRules.Validate(Good(v => v with { CashOnDeliveryEnabled = false })).Errors);
    }

    [Theory]
    [InlineData("myshop", "myshop")]
    [InlineData("@myshop", "myshop")]
    [InlineData("https://www.instagram.com/my.shop/", "my.shop")]
    [InlineData("instagram.com/my_shop", "my_shop")]
    [InlineData("", "")]
    [InlineData("  ", "")]
    public void Instagram_IsNormalized(string input, string expected) => Assert.Equal(expected, StoreSettingsRules.NormalizeInstagram(input));

    [Theory]
    [InlineData("my shop")]
    [InlineData("فروشگاه")]
    [InlineData("a/b")]
    [InlineData("<script>")]
    public void Instagram_RejectsOddInput(string input) => Assert.Null(StoreSettingsRules.NormalizeInstagram(input));

    [Fact]
    public void LongText_IsRejected()
    {
        Assert.Contains("storeAddress", StoreSettingsRules.Validate(Good(v => v with { StoreAddress = new string('ا', 301) })).Errors.Keys);
        Assert.Contains("workingHours", StoreSettingsRules.Validate(Good(v => v with { WorkingHours = new string('ا', 101) })).Errors.Keys);
        Assert.Contains("bankName", StoreSettingsRules.Validate(Good(v => v with { BankName = new string('ا', 51) })).Errors.Keys);
    }

    // ---- what customers are offered ----

    [Fact]
    public void CardToCard_IsOffered_OnlyWhenEnabledAndValidlyConfigured()
    {
        Assert.True(Good().CardToCardOffered);
        Assert.True(Good().IsOffered(PaymentMethod.CardToCard));
        Assert.False(Good(v => v with { CardToCardEnabled = false }).CardToCardOffered);
        Assert.False(Good(v => v with { CardNumber = "" }).CardToCardOffered);
        Assert.False(Good(v => v with { CardNumber = CardNumbers.PlaceholderDigits }).CardToCardOffered);
        Assert.False(Good(v => v with { CardNumber = "6037991234567890" }).CardToCardOffered);
        Assert.False(Good(v => v with { CardHolderName = "" }).CardToCardOffered);
        Assert.False(Good(v => v with { CardHolderName = CardNumbers.PlaceholderHolder }).CardToCardOffered);
    }

    [Fact]
    public void CashOnDelivery_FollowsItsSwitch()
    {
        Assert.True(Good().IsOffered(PaymentMethod.CashOnDelivery));
        Assert.False(Good(v => v with { CashOnDeliveryEnabled = false }).IsOffered(PaymentMethod.CashOnDelivery));
    }
}
