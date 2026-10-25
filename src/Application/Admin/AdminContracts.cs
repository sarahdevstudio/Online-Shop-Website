using FluentValidation;
using Shop.Application.Common;

namespace Shop.Application.Admin;

/// <summary>Counts for the admin dashboard; each links to a filtered list in the UI.</summary>
public sealed record DashboardDto(
    int NewOrders, int TransferReferenceWaiting, int ConfirmedToShip, int OrdersToday, int LowStockVariants, int LowStockThreshold);

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(CancellationToken ct);
}

public sealed record StockAdjustRequest(int Delta, string? Reason);

public sealed record StockAdjustResultDto(int VariantId, int Stock, byte[] RowVersion);

public sealed record StockAdjustmentDto(
    int Id, DateTime AtUtc, Guid? AdminUserId, string? AdminEmail, int Delta, int StockAfter, string? Reason, string Source);

public interface IStockAdminService
{
    /// <summary>One atomic UPDATE (Stock = Stock + delta, never below 0): concurrent checkouts and adjustments can't lose updates.</summary>
    Task<Result<StockAdjustResultDto>> AdjustAsync(int variantId, StockAdjustRequest request, CancellationToken ct);

    Task<Result<IReadOnlyList<StockAdjustmentDto>>> HistoryAsync(int variantId, CancellationToken ct);
}

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().MaximumLength(128);
        RuleFor(x => x.NewPassword).NotEmpty().MaximumLength(128);
    }
}
