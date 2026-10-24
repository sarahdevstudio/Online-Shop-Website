namespace Shop.Domain.Orders;

/// <summary>Shipping rule: one flat fee, free from an optional subtotal threshold (null = no free shipping).</summary>
public static class OrderPricing
{
    public static long ShippingFee(long subtotal, long flatFee, long? freeShippingThreshold) =>
        subtotal <= 0 || (freeShippingThreshold is { } threshold && subtotal >= threshold) ? 0 : flatFee;
}
