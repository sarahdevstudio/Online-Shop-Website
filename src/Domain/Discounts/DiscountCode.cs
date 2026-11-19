using Shop.Domain.Common;

namespace Shop.Domain.Discounts;

public enum DiscountType
{
    Percent = 0,
    Fixed = 1,
}

/// <summary>
/// A discount code the customer types at checkout. Usage limits (<see cref="UsageLimit"/>, <see cref="PerMobileLimit"/>)
/// are enforced by counting non-cancelled orders that reference this code's <see cref="Code"/> (docs/21) — a
/// cancelled order stops counting, which is how a cancellation "releases" its usage, with no separate ledger needed.
/// </summary>
public sealed class DiscountCode
{
    public const int CodeMaxLength = 30;
    public const int CodeMinLength = 3;

    private DiscountCode()
    {
        Code = string.Empty;
    }

    public int Id { get; private set; }
    public string Code { get; private set; }
    public DiscountType Type { get; private set; }

    /// <summary>Percent (1-100) or a fixed whole-Toman amount, depending on <see cref="Type"/>.</summary>
    public long Value { get; private set; }

    public long? MinSubtotal { get; private set; }
    public int? UsageLimit { get; private set; }
    public int PerMobileLimit { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static DiscountCode Create(
        string code, DiscountType type, long value, long? minSubtotal, int? usageLimit, int perMobileLimit,
        DateTime? expiresAtUtc, bool isActive, DateTime utcNow)
    {
        var discount = new DiscountCode { Code = Normalize(code), CreatedAtUtc = utcNow };
        discount.Apply(type, value, minSubtotal, usageLimit, perMobileLimit, expiresAtUtc, isActive, utcNow);
        return discount;
    }

    public void Apply(
        DiscountType type, long value, long? minSubtotal, int? usageLimit, int perMobileLimit,
        DateTime? expiresAtUtc, bool isActive, DateTime utcNow)
    {
        Type = type;
        Value = value;
        MinSubtotal = minSubtotal;
        UsageLimit = usageLimit;
        PerMobileLimit = perMobileLimit;
        ExpiresAtUtc = expiresAtUtc;
        IsActive = isActive;
        UpdatedAtUtc = utcNow;
    }

    public static string Normalize(string code) => code.Trim().ToUpperInvariant();

    /// <summary>Active, not expired, and the cart clears its minimum — everything except the usage counts, which need a
    /// database read the caller does separately (and under the per-code lock, for the checkout path).</summary>
    public bool IsUsable(long subtotal, DateTime utcNow) =>
        IsActive && (ExpiresAtUtc is null || ExpiresAtUtc > utcNow) && (MinSubtotal is null || subtotal >= MinSubtotal);

    /// <summary>Amount off, never more than the subtotal: a code can never make an order's total negative.</summary>
    public long ComputeDiscount(long subtotal)
    {
        var amount = Type == DiscountType.Percent ? subtotal * Value / 100 : Value;
        return Math.Clamp(amount, 0, subtotal);
    }

    /// <summary>Empty when valid, otherwise field → Persian message.</summary>
    public static Dictionary<string, string[]> Validate(string code, DiscountType type, long value, long? minSubtotal, int? usageLimit, int perMobileLimit)
    {
        var errors = new Dictionary<string, string[]>();
        var normalized = Normalize(code);
        if (normalized.Length is < CodeMinLength or > CodeMaxLength || !normalized.All(c => char.IsAsciiLetterOrDigit(c)))
        {
            errors["code"] = [Msg.Of("api.validation.discount-code", ("min", CodeMinLength), ("max", CodeMaxLength))];
        }

        if (type == DiscountType.Percent && value is < 1 or > 100)
        {
            errors["value"] = [Msg.Of("api.validation.discount-value-percent")];
        }

        if (type == DiscountType.Fixed && value < 1)
        {
            errors["value"] = [Msg.Of("api.validation.discount-value-fixed")];
        }

        if (minSubtotal is < 0)
        {
            errors["minSubtotal"] = [Msg.Of("api.validation.discount-min-subtotal")];
        }

        if (usageLimit is < 1)
        {
            errors["usageLimit"] = [Msg.Of("api.validation.discount-usage-limit")];
        }

        if (perMobileLimit < 1)
        {
            errors["perMobileLimit"] = [Msg.Of("api.validation.discount-per-mobile-limit")];
        }

        return errors;
    }
}

/// <summary>Who created, changed or deleted a discount code, and when (the code itself is kept, since a deleted row is gone).</summary>
public sealed class DiscountAuditLog
{
    public const string Created = "created";
    public const string Updated = "updated";
    public const string Deleted = "deleted";

    private DiscountAuditLog()
    {
        Code = Action = string.Empty;
    }

    public int Id { get; private set; }
    public string Code { get; private set; }
    public string Action { get; private set; }
    public Guid? AdminUserId { get; private set; }
    public DateTime AtUtc { get; private set; }

    public static DiscountAuditLog Create(string code, string action, Guid? adminUserId, DateTime utcNow) => new()
    {
        Code = code,
        Action = action,
        AdminUserId = adminUserId,
        AtUtc = utcNow,
    };
}
