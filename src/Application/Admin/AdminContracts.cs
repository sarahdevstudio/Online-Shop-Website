using FluentValidation;
using Shop.Application.Common;

namespace Shop.Application.Admin;

public sealed record DashboardOrdersDto(int NewOrders, int TransferReferenceWaiting, int ConfirmedToShip, int OrdersToday);

public sealed record DashboardReportsDto(long TodayRevenue, long TodayProfit, long MonthRevenue, long MonthProfit, IReadOnlyList<long> Sparkline30d);

public sealed record DashboardLowStockDto(int Count, int Threshold);

/// <summary>
/// The dashboard's cards, each present only when the caller holds its permission (docs/19 F2): a card the account
/// can't see is simply missing from the response, not zeroed or guessed. Every present card links to a filtered
/// admin page.
/// </summary>
public sealed record DashboardDto(
    DashboardOrdersDto? Orders, DashboardReportsDto? Reports, int? PendingReviews, DashboardLowStockDto? LowStock, int? DraftProducts);

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(CancellationToken ct);
}

public sealed record StockAdjustRequest(int Delta, string? Reason);

public sealed record StockAdjustResultDto(int VariantId, int Stock, byte[] RowVersion);

public sealed record StockAdjustmentDto(
    int Id, DateTime AtUtc, Guid? AdminUserId, string? AdminUsername, int Delta, int StockAfter, string? Reason, string Source);

public interface IStockAdminService
{
    /// <summary>One atomic UPDATE (Stock = Stock + delta, never below 0): concurrent checkouts and adjustments can't lose updates.</summary>
    Task<Result<StockAdjustResultDto>> AdjustAsync(int variantId, StockAdjustRequest request, CancellationToken ct);

    Task<Result<IReadOnlyList<StockAdjustmentDto>>> HistoryAsync(int variantId, CancellationToken ct);
}

// ---- reports (docs/19 F2) ----

/// <summary>
/// One period's numbers. Counted orders: Confirmed, Shipped, Delivered (Cancelled and still-pending orders never
/// count). <see cref="Revenue"/> is the sum of item totals of those orders (no discount exists yet: F3 will
/// subtract it once discount codes land). <see cref="CostOfGoods"/>/<see cref="GrossProfit"/> only use order lines
/// whose variant had a cost price at the time of the order; a line with none is skipped, never treated as zero —
/// <see cref="ItemsWithoutCost"/>/<see cref="OrdersWithoutCost"/> say how many lines/orders that affects.
/// </summary>
public sealed record ReportsSummaryDto(
    long Revenue, long ShippingCollected, long CostOfGoods, long GrossProfit, decimal MarginPercent,
    int OrderCount, long AverageOrderValue, int OrdersWithoutCost, int ItemsWithoutCost);

/// <summary>One calendar day (Tehran time). <see cref="Profit"/> only sums lines with a known cost, same rule as the summary.</summary>
public sealed record ReportsDailyPointDto(DateTime Date, long Revenue, long Profit, int OrderCount);

/// <summary>One product's totals over the whole period, for the top-10 tables.</summary>
public sealed record ReportsProductDto(int ProductId, string ProductName, string Slug, long Revenue, long Profit);

public sealed record ReportsDto(
    DateTime FromUtc, DateTime ToUtc, ReportsSummaryDto Summary, IReadOnlyList<ReportsDailyPointDto> Daily,
    IReadOnlyList<ReportsProductDto> TopByRevenue, IReadOnlyList<ReportsProductDto> TopByProfit);

public interface IReportsService
{
    /// <summary>[fromUtc, toUtc): both already converted from the admin's chosen Tehran-local day range.</summary>
    Task<ReportsDto> GetAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct);
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
