using Shop.Domain.Orders;

namespace Shop.Application.Orders;

/// <summary>Checkout settings (<c>Checkout:*</c>). Defaults are placeholders; production sets the real values.</summary>
public sealed class CheckoutOptions
{
    public const string SectionName = "Checkout";
    public const string PlaceholderCardNumber = "6037-0000-0000-0000";

    public int MaxQuantityPerLine { get; set; } = 10;

    /// <summary>Flat shipping fee in Toman (placeholder).</summary>
    public long ShippingFee { get; set; } = 50_000;

    /// <summary>Subtotal from which shipping is free; null disables free shipping.</summary>
    public long? FreeShippingThreshold { get; set; }

    public int MaxUnpaidOrdersPerMobile { get; set; } = 2;
    public int MaxUnpaidOrdersPerIp { get; set; } = 2;

    /// <summary>Orders one IP may place per <see cref="OrderWindowMinutes"/>.</summary>
    public int OrderPermitLimit { get; set; } = 5;

    public int OrderWindowMinutes { get; set; } = 10;
    public int MaxCartLines { get; set; } = 50;

    public CardToCardOptions CardToCard { get; set; } = new();
    public CashOnDeliveryOptions CashOnDelivery { get; set; } = new();

    /// <summary>Problems that must stop a production start: placeholder card details, nonsense limits.</summary>
    public IReadOnlyList<string> ProductionProblems()
    {
        var problems = new List<string>();
        if (CardToCard.Enabled)
        {
            var digits = new string(CardToCard.CardNumber.Where(char.IsAsciiDigit).ToArray());
            if (digits.Length != 16 || CardToCard.CardNumber == PlaceholderCardNumber)
            {
                problems.Add("Checkout:CardToCard:CardNumber must be the real 16-digit card number (it is still the placeholder).");
            }

            if (string.IsNullOrWhiteSpace(CardToCard.HolderName) || CardToCard.HolderName == new CardToCardOptions().HolderName)
            {
                problems.Add("Checkout:CardToCard:HolderName must be the real card holder name.");
            }
        }

        if (!CardToCard.Enabled && !CashOnDelivery.Enabled)
        {
            problems.Add("Enable at least one payment method (Checkout:CardToCard:Enabled or Checkout:CashOnDelivery:Enabled).");
        }

        if (MaxQuantityPerLine < 1 || ShippingFee < 0 || MaxUnpaidOrdersPerMobile < 1 || MaxUnpaidOrdersPerIp < 1)
        {
            problems.Add("Checkout limits and fees are out of range.");
        }

        return problems;
    }

    public bool IsEnabled(PaymentMethod method) => method switch
    {
        PaymentMethod.CardToCard => CardToCard.Enabled,
        PaymentMethod.CashOnDelivery => CashOnDelivery.Enabled,
        _ => false,
    };
}

public sealed class CardToCardOptions
{
    public bool Enabled { get; set; } = true;
    public string CardNumber { get; set; } = CheckoutOptions.PlaceholderCardNumber;
    public string HolderName { get; set; } = "نام صاحب کارت (نمونه)";
}

public sealed class CashOnDeliveryOptions
{
    public bool Enabled { get; set; } = true;
}

// ---- Cart ----

public sealed record PaymentMethodsDto(bool CardToCard, bool CashOnDelivery);

/// <summary>
/// One cart line with the live price. <see cref="Notice"/> is "unavailable", "quantity_exceeds" (more than can be bought)
/// or "price_changed" (<see cref="PreviousPrice"/> is what the customer last saw); null when all is well.
/// </summary>
public sealed record CartLineDto(
    int VariantId, string ProductName, string ProductSlug, string? VariantLabel, string Sku, string? ImageUrl,
    long UnitPrice, int Quantity, int MaxQuantity, long LineTotal, bool Available, string? Notice, long? PreviousPrice);

public sealed record CartDto(
    IReadOnlyList<CartLineDto> Lines, int ItemCount, long Subtotal, long ShippingFee, long Total,
    long? FreeShippingThreshold, bool HasBlockingIssue, PaymentMethodsDto PaymentMethods);

/// <summary><see cref="NewToken"/> is set only when this call created the cart; the storefront stores it in the cookie.</summary>
public sealed record CartMutationDto(string? NewToken, CartDto Cart, bool Clamped);

public sealed record CartCountDto(int Count);

public sealed record AddCartItemRequest(int VariantId, int Quantity);

public sealed record UpdateCartItemRequest(int Quantity);

// ---- Orders (customer) ----

public sealed record PlaceOrderRequest(
    string FullName, string Mobile, string Province, string City, string Address, string PostalCode, string? Note,
    PaymentMethod PaymentMethod, long ExpectedTotal);

/// <summary>What the transport layer knows: the cart cookie value, the idempotency key header and the client address.</summary>
public sealed record PlaceOrderContext(string? CartToken, Guid IdempotencyKey, string ClientIp);

public sealed record PlacedOrderDto(string AccessToken, int Number, bool Replayed);

public sealed record StockShortageDto(int VariantId, string ProductName, string? VariantLabel, int Requested, int Available);

public sealed record OrderLineDto(string ProductName, string? VariantLabel, string Sku, long UnitPrice, int Quantity, long LineTotal);

public sealed record PaymentInstructionsDto(string CardNumber, string HolderName, long Amount);

public sealed record TransferReferenceDto(string Tracking, string Time, string CardLast4);

public sealed record OrderDto(
    int Number, OrderStatus Status, PaymentStatus PaymentStatus, PaymentMethod PaymentMethod, DateTime CreatedAtUtc,
    string FullName, string Mobile, string Province, string City, string Address, string PostalCode, string? Note,
    IReadOnlyList<OrderLineDto> Lines, long Subtotal, long ShippingFee, long Total,
    PaymentInstructionsDto? PaymentInstructions, TransferReferenceDto? TransferReference);

public sealed record TransferReferenceRequest(string Tracking, string Time, string CardLast4);

// ---- Orders (admin) ----

public sealed record AdminOrderListItemDto(
    int Id, int Number, OrderStatus Status, PaymentStatus PaymentStatus, PaymentMethod PaymentMethod,
    string FullName, string Mobile, long Total, DateTime CreatedAtUtc, bool HasTransferReference);

public sealed record OrderStatusLogDto(OrderStatus? From, OrderStatus To, Guid? AdminUserId, DateTime AtUtc);

public sealed record AdminOrderDto(
    int Id, OrderDto Order, string ClientIp, IReadOnlyList<OrderStatusLogDto> StatusLog);

public sealed record SetOrderStatusRequest(OrderStatus Status);
