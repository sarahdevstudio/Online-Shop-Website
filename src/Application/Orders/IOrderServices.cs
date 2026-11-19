using Shop.Application.Catalog;
using Shop.Application.Common;
using Shop.Domain.Orders;

namespace Shop.Application.Orders;

/// <summary>The guest cart. The token is the raw cookie value; unknown or expired tokens behave like an empty cart.</summary>
public interface ICartService
{
    Task<CartDto> GetAsync(string? token, CancellationToken ct);

    Task<int> CountAsync(string? token, CancellationToken ct);

    Task<Result<CartMutationDto>> AddItemAsync(string? token, AddCartItemRequest request, CancellationToken ct);

    /// <summary>Quantity 0 removes the line.</summary>
    Task<Result<CartMutationDto>> SetQuantityAsync(string? token, int variantId, int quantity, CancellationToken ct);

    /// <summary>Validates the code exists, is active and not expired (usage limits are checked again, under lock, at
    /// placement) and attaches it to the cart. A cart must already exist (non-empty).</summary>
    Task<Result<CartDto>> ApplyDiscountAsync(string? token, string code, CancellationToken ct);

    Task<CartDto> RemoveDiscountAsync(string? token, CancellationToken ct);
}

public interface IOrderService
{
    /// <summary>
    /// Places the order for the cart in one transaction. Errors: <c>cart_empty</c>, <c>unpaid_limit</c>,
    /// <c>stock_insufficient</c> (with <c>lines</c>), <c>price_changed</c>, or validation failures.
    /// </summary>
    Task<Result<PlacedOrderDto>> PlaceAsync(PlaceOrderRequest request, PlaceOrderContext context, CancellationToken ct);

    Task<Result<OrderDto>> GetAsync(string accessToken, CancellationToken ct);

    Task<Result<OrderDto>> SubmitTransferReferenceAsync(string accessToken, TransferReferenceRequest request, CancellationToken ct);
}

public interface IAdminOrderService
{
    Task<PagedList<AdminOrderListItemDto>> ListAsync(OrderStatus? status, PaymentStatus? paymentStatus, string? q, int page, CancellationToken ct);

    Task<Result<AdminOrderDto>> GetAsync(int id, CancellationToken ct);

    Task<Result<AdminOrderDto>> ConfirmPaymentAsync(int id, CancellationToken ct);

    Task<Result<AdminOrderDto>> SetStatusAsync(int id, OrderStatus status, CancellationToken ct);

    /// <summary>Cancels and restores the stock in the same transaction; allowed until the order is shipped.</summary>
    Task<Result<AdminOrderDto>> CancelAsync(int id, CancellationToken ct);
}
