namespace Shop.Domain.Inventory;

/// <summary>
/// One admin-made stock change. Customer purchases and cancellations are tracked by orders, not here. <see cref="Source"/> is
/// "adjust" (add/remove N, atomic) or "edit" (the exact value typed into the variant form).
/// </summary>
public sealed class StockAdjustment
{
    public const int ReasonMaxLength = 200;
    public const string AdjustSource = "adjust";
    public const string EditSource = "edit";

    private StockAdjustment()
    {
        Source = AdjustSource;
    }

    public int Id { get; private set; }
    public int VariantId { get; private set; }
    public Guid? AdminUserId { get; private set; }
    public int Delta { get; private set; }
    public int StockAfter { get; private set; }
    public string? Reason { get; private set; }
    public string Source { get; private set; }
    public DateTime AtUtc { get; private set; }

    public static StockAdjustment Create(int variantId, Guid? adminUserId, int delta, int stockAfter, string? reason, string source, DateTime utcNow) => new()
    {
        VariantId = variantId,
        AdminUserId = adminUserId,
        Delta = delta,
        StockAfter = stockAfter,
        Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()[..Math.Min(reason.Trim().Length, ReasonMaxLength)],
        Source = source,
        AtUtc = utcNow,
    };
}
