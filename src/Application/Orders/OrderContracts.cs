using Shop.Domain.Orders;

namespace Shop.Application.Orders;

/// <summary>
/// Checkout limits that stay in configuration (<c>Checkout:*</c>). Everything the store owner edits (shipping fee,
/// payment methods, card details, quantity limit, contact info) lives in the database settings instead.
/// </summary>
public sealed class CheckoutOptions
{
    public const string SectionName = "Checkout";

    public int MaxUnpaidOrdersPerMobile { get; set; } = 2;
    public int MaxUnpaidOrdersPerIp { get; set; } = 2;

    /// <summary>Orders one IP may place per <see cref="OrderWindowMinutes"/>.</summary>
    public int OrderPermitLimit { get; set; } = 5;

    public int OrderWindowMinutes { get; set; } = 10;
    public int MaxCartLines { get; set; } = 50;
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

public sealed record PaymentInstructionsDto(string CardNumber, string HolderName, string BankName, long Amount);

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
