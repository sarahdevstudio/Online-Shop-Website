namespace Shop.Domain.Orders;

/// <summary>
/// A placed order. Lines and amounts are snapshots; status changes go through the methods below so the allowed
/// transitions live in one place. <see cref="RowVersion"/> makes two concurrent changes (e.g. cancel twice) fail.
/// </summary>
public sealed class Order
{
    public const int NameMaxLength = 100;
    public const int ProvinceMaxLength = 50;
    public const int CityMaxLength = 50;
    public const int AddressMaxLength = 500;
    public const int NoteMaxLength = 500;
    public const int TrackingMaxLength = 30;
    public const int TransferTimeMaxLength = 50;
    public const int AccessTokenLength = 43;
    public const int ClientIpMaxLength = 45;

    private Order()
    {
        AccessToken = FullName = Mobile = Province = City = Address = PostalCode = ClientIp = string.Empty;
    }

    public int Id { get; private set; }

    /// <summary>Human-readable sequential number, assigned by the database sequence.</summary>
    public int Number { get; private set; }

    public string AccessToken { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public OrderStatus Status { get; private set; }
    public PaymentStatus PaymentStatus { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }

    public string FullName { get; private set; }
    public string Mobile { get; private set; }
    public string Province { get; private set; }
    public string City { get; private set; }
    public string Address { get; private set; }
    public string PostalCode { get; private set; }
    public string? Note { get; private set; }
    public string ClientIp { get; private set; }

    public long Subtotal { get; private set; }
    public long ShippingFee { get; private set; }
    public long Total { get; private set; }

    public string? TransferTracking { get; private set; }
    public string? TransferTime { get; private set; }
    public string? TransferCardLast4 { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public List<OrderItem> Items { get; private set; } = [];
    public List<OrderStatusLog> StatusLog { get; private set; } = [];

    /// <summary>Statuses in which the order still counts against the per-mobile / per-IP unpaid limit.</summary>
    public static readonly OrderStatus[] UnpaidStatuses = [OrderStatus.AwaitingPayment, OrderStatus.Pending];

    public static Order Create(
        string accessToken, Guid idempotencyKey, PaymentMethod method,
        string fullName, string mobile, string province, string city, string address, string postalCode, string? note,
        string clientIp, long subtotal, long shippingFee, IEnumerable<OrderItem> items, DateTime utcNow)
    {
        if (!Enum.IsDefined(method))
        {
            throw new ArgumentOutOfRangeException(nameof(method));
        }

        if (subtotal < 0 || shippingFee < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(subtotal));
        }

        var initial = method == PaymentMethod.CardToCard ? OrderStatus.AwaitingPayment : OrderStatus.Pending;
        var order = new Order
        {
            AccessToken = accessToken,
            IdempotencyKey = idempotencyKey,
            PaymentMethod = method,
            Status = initial,
            PaymentStatus = PaymentStatus.Unpaid,
            FullName = fullName,
            Mobile = mobile,
            Province = province,
            City = city,
            Address = address,
            PostalCode = postalCode,
            Note = string.IsNullOrWhiteSpace(note) ? null : note,
            ClientIp = clientIp,
            Subtotal = subtotal,
            ShippingFee = shippingFee,
            Total = subtotal + shippingFee,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
        order.Items.AddRange(items);
        if (order.Items.Count == 0)
        {
            throw new ArgumentException("An order needs at least one line.", nameof(items));
        }

        order.StatusLog.Add(OrderStatusLog.Create(null, initial, null, utcNow));
        return order;
    }

    /// <summary>Card-to-card: the admin has seen the transfer. Allowed only while awaiting payment.</summary>
    public bool TryConfirmPayment(Guid? adminUserId, DateTime utcNow)
    {
        if (PaymentMethod != PaymentMethod.CardToCard || Status != OrderStatus.AwaitingPayment)
        {
            return false;
        }

        PaymentStatus = PaymentStatus.Paid;
        MoveTo(OrderStatus.Confirmed, adminUserId, utcNow);
        return true;
    }

    /// <summary>Pending → Confirmed → Shipped → Delivered. Cash on delivery becomes Paid on delivery.</summary>
    public bool TryAdvance(OrderStatus target, Guid? adminUserId, DateTime utcNow)
    {
        var allowed = (Status, target) switch
        {
            (OrderStatus.Pending, OrderStatus.Confirmed) => true,
            (OrderStatus.Confirmed, OrderStatus.Shipped) => true,
            (OrderStatus.Shipped, OrderStatus.Delivered) => true,
            _ => false,
        };
        if (!allowed)
        {
            return false;
        }

        if (target == OrderStatus.Delivered && PaymentMethod == PaymentMethod.CashOnDelivery)
        {
            PaymentStatus = PaymentStatus.Paid;
        }

        MoveTo(target, adminUserId, utcNow);
        return true;
    }

    /// <summary>Allowed until the order is shipped. The caller restores the stock in the same transaction.</summary>
    public bool TryCancel(Guid? adminUserId, DateTime utcNow)
    {
        if (Status is not (OrderStatus.AwaitingPayment or OrderStatus.Pending or OrderStatus.Confirmed))
        {
            return false;
        }

        MoveTo(OrderStatus.Cancelled, adminUserId, utcNow);
        return true;
    }

    /// <summary>The customer's transfer reference; may be replaced until the payment is confirmed.</summary>
    public bool TrySetTransferReference(string tracking, string time, string cardLast4, DateTime utcNow)
    {
        if (PaymentMethod != PaymentMethod.CardToCard || Status != OrderStatus.AwaitingPayment)
        {
            return false;
        }

        TransferTracking = tracking;
        TransferTime = time;
        TransferCardLast4 = cardLast4;
        UpdatedAtUtc = utcNow;
        return true;
    }

    private void MoveTo(OrderStatus target, Guid? adminUserId, DateTime utcNow)
    {
        StatusLog.Add(OrderStatusLog.Create(Status, target, adminUserId, utcNow));
        Status = target;
        UpdatedAtUtc = utcNow;
    }
}

public sealed class OrderItem
{
    public const int ProductNameMaxLength = 200;
    public const int VariantLabelMaxLength = 300;

    private OrderItem()
    {
        ProductName = Sku = string.Empty;
    }

    public int Id { get; private set; }
    public int OrderId { get; private set; }
    public int VariantId { get; private set; }
    public string ProductName { get; private set; }
    public string? VariantLabel { get; private set; }
    public string Sku { get; private set; }
    public long UnitPrice { get; private set; }
    public int Quantity { get; private set; }

    public long LineTotal => UnitPrice * Quantity;

    public static OrderItem Create(int variantId, string productName, string? variantLabel, string sku, long unitPrice, int quantity) => new()
    {
        VariantId = variantId,
        ProductName = productName.Length <= ProductNameMaxLength ? productName : productName[..ProductNameMaxLength],
        VariantLabel = variantLabel is { Length: > VariantLabelMaxLength } ? variantLabel[..VariantLabelMaxLength] : variantLabel,
        Sku = sku,
        UnitPrice = unitPrice,
        Quantity = quantity,
    };
}

/// <summary>Who moved an order from one status to another, and when. <see cref="FromStatus"/> is null for creation.</summary>
public sealed class OrderStatusLog
{
    private OrderStatusLog()
    {
    }

    public int Id { get; private set; }
    public int OrderId { get; private set; }
    public OrderStatus? FromStatus { get; private set; }
    public OrderStatus ToStatus { get; private set; }
    public Guid? AdminUserId { get; private set; }
    public DateTime AtUtc { get; private set; }

    public static OrderStatusLog Create(OrderStatus? from, OrderStatus to, Guid? adminUserId, DateTime utcNow) =>
        new() { FromStatus = from, ToStatus = to, AdminUserId = adminUserId, AtUtc = utcNow };
}
