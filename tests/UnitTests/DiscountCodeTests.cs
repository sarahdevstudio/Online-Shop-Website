using Shop.Domain.Discounts;

namespace Shop.UnitTests;

public class DiscountCodeTests
{
    private static readonly DateTime T0 = new(2026, 10, 26, 8, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(DiscountType.Percent, 10, 200_000, 20_000)]
    [InlineData(DiscountType.Percent, 100, 200_000, 200_000)] // never more than the subtotal
    [InlineData(DiscountType.Fixed, 50_000, 200_000, 50_000)]
    [InlineData(DiscountType.Fixed, 999_999, 10_000, 10_000)] // a fixed amount bigger than the cart is clamped, never negative
    public void ComputeDiscount_NeverExceedsTheSubtotal(DiscountType type, long value, long subtotal, long expected)
    {
        var code = DiscountCode.Create("SAVE10", type, value, null, null, 1, null, true, T0);
        Assert.Equal(expected, code.ComputeDiscount(subtotal));
    }

    [Fact]
    public void IsUsable_FalseWhenInactive_Expired_OrBelowTheMinimum()
    {
        var active = DiscountCode.Create("A", DiscountType.Fixed, 1000, null, null, 1, null, true, T0);
        Assert.True(active.IsUsable(10_000, T0));

        var inactive = DiscountCode.Create("B", DiscountType.Fixed, 1000, null, null, 1, null, false, T0);
        Assert.False(inactive.IsUsable(10_000, T0));

        var expired = DiscountCode.Create("C", DiscountType.Fixed, 1000, null, null, 1, T0.AddDays(-1), true, T0);
        Assert.False(expired.IsUsable(10_000, T0));

        var notExpiredYet = DiscountCode.Create("D", DiscountType.Fixed, 1000, null, null, 1, T0.AddDays(1), true, T0);
        Assert.True(notExpiredYet.IsUsable(10_000, T0));

        var minimum = DiscountCode.Create("E", DiscountType.Fixed, 1000, 50_000, null, 1, null, true, T0);
        Assert.False(minimum.IsUsable(49_999, T0));
        Assert.True(minimum.IsUsable(50_000, T0));
    }

    [Fact]
    public void Normalize_TrimsAndUppercases()
    {
        Assert.Equal("SAVE10", DiscountCode.Normalize("  save10  "));
    }

    [Theory]
    [InlineData("AB", DiscountType.Percent, 10, null, null, 1, true)] // too short
    [InlineData("SAVE-10", DiscountType.Percent, 10, null, null, 1, true)] // a hyphen is not alphanumeric
    [InlineData("SAVE10", DiscountType.Percent, 0, null, null, 1, true)] // 0% is not a discount
    [InlineData("SAVE10", DiscountType.Percent, 101, null, null, 1, true)] // over 100%
    [InlineData("SAVE10", DiscountType.Fixed, 0, null, null, 1, true)]
    [InlineData("SAVE10", DiscountType.Fixed, 50_000, -1L, null, 1, true)] // negative minimum
    [InlineData("SAVE10", DiscountType.Fixed, 50_000, null, 0, 1, true)] // a usage limit of 0 makes no sense
    [InlineData("SAVE10", DiscountType.Fixed, 50_000, null, null, 0, true)] // per-mobile limit must be at least 1
    [InlineData("SAVE10", DiscountType.Percent, 10, 100_000L, 5, 2, false)]
    public void Validate_ReportsEachBadField(string code, DiscountType type, long value, long? minSubtotal, int? usageLimit, int perMobileLimit, bool hasErrors)
    {
        var errors = DiscountCode.Validate(code, type, value, minSubtotal, usageLimit, perMobileLimit);
        Assert.Equal(hasErrors, errors.Count > 0);
    }
}
