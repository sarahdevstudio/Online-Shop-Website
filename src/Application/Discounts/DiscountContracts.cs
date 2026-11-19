using Shop.Application.Common;
using Shop.Domain.Discounts;

namespace Shop.Application.Discounts;

public sealed record AdminDiscountListItemDto(
    int Id, string Code, DiscountType Type, long Value, long? MinSubtotal, int? UsageLimit, int UsedCount,
    int PerMobileLimit, DateTime? ExpiresAtUtc, bool IsActive);

public sealed record AdminDiscountDto(
    int Id, string Code, DiscountType Type, long Value, long? MinSubtotal, int? UsageLimit, int UsedCount,
    int PerMobileLimit, DateTime? ExpiresAtUtc, bool IsActive, DateTime CreatedAtUtc, byte[] RowVersion);

public sealed record CreateDiscountCodeRequest(
    string Code, DiscountType Type, long Value, long? MinSubtotal, int? UsageLimit, int PerMobileLimit, DateTime? ExpiresAtUtc, bool IsActive);

/// <summary>The code never changes after creation (it may already be on placed orders).</summary>
public sealed record UpdateDiscountCodeRequest(
    DiscountType Type, long Value, long? MinSubtotal, int? UsageLimit, int PerMobileLimit, DateTime? ExpiresAtUtc, bool IsActive, byte[] RowVersion);

public sealed record DiscountAuditEntryDto(DateTime AtUtc, string Action, string? AdminUsername);

public interface IAdminDiscountService
{
    Task<IReadOnlyList<AdminDiscountListItemDto>> ListAsync(CancellationToken ct);

    Task<Result<AdminDiscountDto>> GetAsync(int id, CancellationToken ct);

    Task<Result<AdminDiscountDto>> CreateAsync(CreateDiscountCodeRequest request, CancellationToken ct);

    Task<Result<AdminDiscountDto>> UpdateAsync(int id, UpdateDiscountCodeRequest request, CancellationToken ct);

    Task<Result> DeleteAsync(int id, CancellationToken ct);

    Task<Result<IReadOnlyList<DiscountAuditEntryDto>>> AuditAsync(int id, CancellationToken ct);
}

// ---- Cart-facing (public, under /store/cart/discount) ----

/// <summary><see cref="Reason"/> is set, and <see cref="Amount"/> is 0, when the applied code is no longer usable
/// (expired, deactivated, or the cart dropped below its minimum) — the cart keeps the code so the customer can see
/// why, rather than silently losing their entry.</summary>
public sealed record CartDiscountDto(string Code, long Amount, string? Reason);

public sealed record ApplyDiscountRequest(string Code);
