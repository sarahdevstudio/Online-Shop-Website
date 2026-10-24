namespace Shop.Domain.Orders;

/// <summary>A guest cart, identified by the hash of a random token that only the customer's cookie holds.</summary>
public sealed class Cart
{
    public const int ExpiryDays = 30;

    private Cart()
    {
        TokenHash = [];
    }

    public int Id { get; private set; }
    public byte[] TokenHash { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime LastActivityAtUtc { get; private set; }

    public List<CartItem> Items { get; private set; } = [];

    public static Cart Create(byte[] tokenHash, DateTime utcNow) =>
        new() { TokenHash = tokenHash, CreatedAtUtc = utcNow, LastActivityAtUtc = utcNow };

    public void Touch(DateTime utcNow) => LastActivityAtUtc = utcNow;
}

/// <summary>
/// One cart line. The price is never stored for charging: <see cref="SeenPrice"/> only remembers what the customer last
/// saw, so the cart can warn that it changed.
/// </summary>
public sealed class CartItem
{
    private CartItem()
    {
    }

    public int Id { get; private set; }
    public int CartId { get; private set; }
    public int VariantId { get; private set; }
    public int Quantity { get; private set; }
    public long SeenPrice { get; private set; }

    public static CartItem Create(int variantId, int quantity, long seenPrice) =>
        new() { VariantId = variantId, Quantity = quantity, SeenPrice = seenPrice };

    public void Set(int quantity, long seenPrice)
    {
        Quantity = quantity;
        SeenPrice = seenPrice;
    }
}
