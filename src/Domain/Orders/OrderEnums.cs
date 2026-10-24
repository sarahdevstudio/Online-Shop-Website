namespace Shop.Domain.Orders;

public enum OrderStatus
{
    /// <summary>Card-to-card order waiting for the transfer to be confirmed by an admin.</summary>
    AwaitingPayment = 0,

    /// <summary>Cash-on-delivery order waiting for an admin to confirm it.</summary>
    Pending = 1,
    Confirmed = 2,
    Shipped = 3,
    Delivered = 4,
    Cancelled = 5,
}

public enum PaymentStatus
{
    Unpaid = 0,
    Paid = 1,
}

public enum PaymentMethod
{
    CardToCard = 0,
    CashOnDelivery = 1,
}
