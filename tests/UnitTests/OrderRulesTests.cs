using Shop.Application.Orders;
using Shop.Domain.Orders;

namespace Shop.UnitTests;

public class OrderRulesTests
{
    private static readonly DateTime Now = new(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);

    private static Order NewOrder(PaymentMethod method) => Order.Create(
        "t".PadRight(43, 'x'), Guid.NewGuid(), method, "علی", "09123456789", "تهران", "تهران", "خیابان ولیعصر پلاک ۱", "1234567890", null,
        "10.0.0.1", 200_000, 50_000,
        method == PaymentMethod.CardToCard ? new CardSnapshot("6037991234567893", "رضا احمدی", "ملی") : null,
        [OrderItem.Create(1, "لامپ", "۹ وات", "SKU-1", 100_000, 2)], Now);

    // ---- shipping ----

    [Theory]
    [InlineData(100_000, 50_000, null, 50_000)] // no free-shipping threshold
    [InlineData(999_999_999, 50_000, null, 50_000)]
    [InlineData(1_999_999L, 50_000L, 2_000_000L, 50_000L)] // below the threshold
    [InlineData(2_000_000L, 50_000L, 2_000_000L, 0L)] // at the threshold
    [InlineData(3_000_000L, 50_000L, 2_000_000L, 0L)]
    [InlineData(0, 50_000, null, 0)] // nothing to ship
    public void ShippingFee_FollowsFlatFeeAndThreshold(long subtotal, long fee, long? threshold, long expected) =>
        Assert.Equal(expected, OrderPricing.ShippingFee(subtotal, fee, threshold));

    // ---- mobile / postal ----

    [Theory]
    [InlineData("09123456789", "09123456789")]
    [InlineData("۰۹۱۲۳۴۵۶۷۸۹", "09123456789")]
    [InlineData("٠٩١٢٣٤٥٦٧٨٩", "09123456789")] // Arabic-Indic digits
    [InlineData("  0912 345 6789 ", "09123456789")]
    [InlineData("0912-345-6789", "09123456789")]
    [InlineData("+989123456789", "09123456789")]
    [InlineData("+98 912 345 6789", "09123456789")]
    [InlineData("00989123456789", "09123456789")]
    [InlineData("989123456789", "09123456789")]
    [InlineData("9123456789", "09123456789")]
    public void Mobile_IsNormalized(string input, string expected)
    {
        Assert.True(IranianMobile.TryNormalize(input, out var mobile));
        Assert.Equal(expected, mobile);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0912345678")] // too short
    [InlineData("091234567890")] // too long
    [InlineData("02112345678")] // landline
    [InlineData("08123456789")]
    [InlineData("09123abc789")]
    [InlineData("12345")]
    public void Mobile_RejectsOtherInput(string? input) => Assert.False(IranianMobile.TryNormalize(input, out _));

    [Theory]
    [InlineData("1234567890", true)]
    [InlineData("۱۲۳۴۵۶۷۸۹۰", true)]
    [InlineData("123456789", false)]
    [InlineData("12345678901", false)]
    [InlineData("12345-6789", false)]
    [InlineData("", false)]
    public void PostalCode_MustBeTenDigits(string input, bool valid) => Assert.Equal(valid, PostalCodes.TryNormalize(input, out _));

    // ---- order lifecycle ----

    [Fact]
    public void NewOrder_StartsInTheStatusForItsPaymentMethod_WithAnInitialLogEntry()
    {
        var card = NewOrder(PaymentMethod.CardToCard);
        var cod = NewOrder(PaymentMethod.CashOnDelivery);

        Assert.Equal(OrderStatus.AwaitingPayment, card.Status);
        Assert.Equal(OrderStatus.Pending, cod.Status);
        Assert.Equal(PaymentStatus.Unpaid, card.PaymentStatus);
        Assert.Equal(250_000, card.Total);
        var entry = Assert.Single(card.StatusLog);
        Assert.Null(entry.FromStatus);
        Assert.Equal(OrderStatus.AwaitingPayment, entry.ToStatus);
    }

    [Fact]
    public void ConfirmPayment_OnlyForCardToCardAwaitingPayment()
    {
        var card = NewOrder(PaymentMethod.CardToCard);
        var cod = NewOrder(PaymentMethod.CashOnDelivery);

        Assert.False(cod.TryConfirmPayment(null, Now));
        Assert.True(card.TryConfirmPayment(Guid.NewGuid(), Now));
        Assert.Equal(OrderStatus.Confirmed, card.Status);
        Assert.Equal(PaymentStatus.Paid, card.PaymentStatus);
        Assert.False(card.TryConfirmPayment(null, Now)); // not twice
    }

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Confirmed, true)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Shipped, true)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Delivered, true)]
    [InlineData(OrderStatus.Pending, OrderStatus.Shipped, false)]
    [InlineData(OrderStatus.Pending, OrderStatus.Delivered, false)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Delivered, false)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Confirmed, false)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Shipped, false)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled, false)] // cancelling has its own method
    [InlineData(OrderStatus.AwaitingPayment, OrderStatus.Confirmed, false)] // must go through payment confirmation
    public void Advance_FollowsTheTransitionTable(OrderStatus from, OrderStatus to, bool allowed)
    {
        var order = NewOrder(PaymentMethod.CashOnDelivery);
        Reach(order, from);

        Assert.Equal(allowed, order.TryAdvance(to, null, Now));
        Assert.Equal(allowed ? to : from, order.Status);
    }

    [Fact]
    public void CashOnDelivery_BecomesPaid_WhenDelivered()
    {
        var order = NewOrder(PaymentMethod.CashOnDelivery);
        Reach(order, OrderStatus.Shipped);
        Assert.Equal(PaymentStatus.Unpaid, order.PaymentStatus);

        Assert.True(order.TryAdvance(OrderStatus.Delivered, null, Now));

        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
    }

    [Theory]
    [InlineData(OrderStatus.Pending, true)]
    [InlineData(OrderStatus.Confirmed, true)]
    [InlineData(OrderStatus.Shipped, false)]
    [InlineData(OrderStatus.Delivered, false)]
    [InlineData(OrderStatus.Cancelled, false)]
    public void Cancel_IsAllowedOnlyUntilShipped(OrderStatus at, bool allowed)
    {
        var order = NewOrder(PaymentMethod.CashOnDelivery);
        Reach(order, at);

        Assert.Equal(allowed, order.TryCancel(null, Now));
    }

    [Fact]
    public void Cancel_FromAwaitingPayment_IsAllowed_AndIsLogged()
    {
        var order = NewOrder(PaymentMethod.CardToCard);
        var admin = Guid.NewGuid();

        Assert.True(order.TryCancel(admin, Now));

        var last = order.StatusLog.Last();
        Assert.Equal(OrderStatus.AwaitingPayment, last.FromStatus);
        Assert.Equal(OrderStatus.Cancelled, last.ToStatus);
        Assert.Equal(admin, last.AdminUserId);
    }

    [Fact]
    public void TransferReference_OnlyWhileAwaitingCardPayment()
    {
        var card = NewOrder(PaymentMethod.CardToCard);
        var cod = NewOrder(PaymentMethod.CashOnDelivery);

        Assert.False(cod.TrySetTransferReference("12345678", "now", "1234", Now));
        Assert.True(card.TrySetTransferReference("12345678", "now", "1234", Now));
        Assert.True(card.TrySetTransferReference("87654321", "later", "4321", Now)); // may be corrected
        Assert.Equal("87654321", card.TransferTracking);
        card.TryConfirmPayment(null, Now);
        Assert.False(card.TrySetTransferReference("11111111", "x", "0000", Now));
    }

    [Fact]
    public void Order_RequiresLines_AndNonNegativeAmounts()
    {
        Assert.Throws<ArgumentException>(() => Order.Create("t", Guid.NewGuid(), PaymentMethod.CardToCard, "a", "09123456789", "p", "c", "addr", "1234567890", null, "ip", 1, 0, null, [], Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => Order.Create("t", Guid.NewGuid(), PaymentMethod.CardToCard, "a", "09123456789", "p", "c", "addr", "1234567890", null, "ip", -1, 0, null, [OrderItem.Create(1, "n", null, "s", 1, 1)], Now));
    }

    // ---- storefront key ----

    [Fact]
    public void StorefrontKey_Matches_OnlyTheExactValue()
    {
        const string key = "0123456789abcdef0123456789abcdef";

        Assert.True(StorefrontKeyRules.Matches(key, key));
        Assert.False(StorefrontKeyRules.Matches(key, key + "x"));
        Assert.False(StorefrontKeyRules.Matches(key, key[..^1]));
        Assert.False(StorefrontKeyRules.Matches(key, key.ToUpperInvariant()));
        Assert.False(StorefrontKeyRules.Matches(key, ""));
        Assert.False(StorefrontKeyRules.Matches(key, null));
        Assert.False(StorefrontKeyRules.Matches(null, null)); // an unconfigured key never matches an empty header
        Assert.False(StorefrontKeyRules.Matches("", ""));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("short", false)]
    [InlineData("dev-storefront-key-not-a-secret-0123456789", false)] // development key
    [InlineData("test-storefront-key-0123456789abcdef-xyz", false)]
    [InlineData("change-me-change-me-change-me-12345678", false)]
    [InlineData("this-is-a-placeholder-value-for-the-key", false)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", false)] // no entropy
    [InlineData("k7Qx9mZp2VbN4tLw8RsY1uHd6GfJ3cAe", true)]
    [InlineData("Zk3-Vq9_Lm2Xr7Tn5Hb8Wd4Yc1Pf6Sg0Ja", true)]
    public void StorefrontKey_ProductionRules(string? key, bool acceptable) =>
        Assert.Equal(acceptable, StorefrontKeyRules.IsAcceptableForProduction(key));

    private static void Reach(Order order, OrderStatus target)
    {
        switch (target)
        {
            case OrderStatus.Pending:
                break;
            case OrderStatus.Confirmed:
                order.TryAdvance(OrderStatus.Confirmed, null, Now);
                break;
            case OrderStatus.Shipped:
                order.TryAdvance(OrderStatus.Confirmed, null, Now);
                order.TryAdvance(OrderStatus.Shipped, null, Now);
                break;
            case OrderStatus.Delivered:
                order.TryAdvance(OrderStatus.Confirmed, null, Now);
                order.TryAdvance(OrderStatus.Shipped, null, Now);
                order.TryAdvance(OrderStatus.Delivered, null, Now);
                break;
            case OrderStatus.Cancelled:
                order.TryCancel(null, Now);
                break;
            case OrderStatus.AwaitingPayment:
                // A cash-on-delivery order never awaits payment; model it with a card order instead.
                typeof(Order).GetProperty(nameof(Order.Status))!.SetValue(order, OrderStatus.AwaitingPayment);
                break;
        }
    }
}
