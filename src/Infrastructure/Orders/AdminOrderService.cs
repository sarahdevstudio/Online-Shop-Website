using Microsoft.EntityFrameworkCore;
using Shop.Application.Catalog;
using Shop.Application.Common;
using Shop.Application.Orders;
using Shop.Domain.Common;
using Shop.Domain.Orders;
using Shop.Infrastructure.Persistence;

namespace Shop.Infrastructure.Orders;

internal sealed class AdminOrderService(
    ShopDbContext db, IStockService stock, ICurrentUser currentUser, TimeProvider clock)
    : IAdminOrderService
{
    private const int PageSize = 20;

    public async Task<PagedList<AdminOrderListItemDto>> ListAsync(
        OrderStatus? status, PaymentStatus? paymentStatus, string? q, int page, CancellationToken ct)
    {
        var query = db.Orders.AsNoTracking().AsQueryable();
        if (status is { } s)
        {
            query = query.Where(o => o.Status == s);
        }

        if (paymentStatus is { } p)
        {
            query = query.Where(o => o.PaymentStatus == p);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var digits = Digits.ToLatin(q);
            var name = PersianText.Normalize(q);
            query = int.TryParse(digits, out var number)
                ? query.Where(o => o.Number == number || o.Mobile.Contains(digits))
                : query.Where(o => o.Mobile.Contains(digits) || o.FullName.Contains(name));
        }

        page = Math.Max(page, 1);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(o => o.CreatedAtUtc).ThenByDescending(o => o.Id)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(o => new AdminOrderListItemDto(
                o.Id, o.Number, o.Status, o.PaymentStatus, o.PaymentMethod, o.FullName, o.Mobile, o.Total, o.CreatedAtUtc,
                o.TransferTracking != null))
            .ToListAsync(ct);
        return new PagedList<AdminOrderListItemDto>(items, page, PageSize, total);
    }

    public async Task<Result<AdminOrderDto>> GetAsync(int id, CancellationToken ct)
    {
        var order = await LoadAsync(id, track: false, ct);
        return order is null ? NotFound() : ToDto(order);
    }

    public Task<Result<AdminOrderDto>> ConfirmPaymentAsync(int id, CancellationToken ct) =>
        ChangeAsync(id, (order, now) => order.TryConfirmPayment(currentUser.UserId, now), restoreStock: false, ct);

    public Task<Result<AdminOrderDto>> SetStatusAsync(int id, OrderStatus status, CancellationToken ct) =>
        status is OrderStatus.Confirmed or OrderStatus.Shipped or OrderStatus.Delivered
            ? ChangeAsync(id, (order, now) => order.TryAdvance(status, currentUser.UserId, now), restoreStock: false, ct)
            : Task.FromResult<Result<AdminOrderDto>>(Error.Conflict("status_not_allowed", "این تغییر وضعیت مجاز نیست."));

    public Task<Result<AdminOrderDto>> CancelAsync(int id, CancellationToken ct) =>
        ChangeAsync(id, (order, now) => order.TryCancel(currentUser.UserId, now), restoreStock: true, ct);

    /// <summary>
    /// Applies one guarded transition. The row version makes two simultaneous changes (e.g. two cancels) fail, so the
    /// stock is restored exactly once, in the same transaction as the status change.
    /// </summary>
    private async Task<Result<AdminOrderDto>> ChangeAsync(
        int id, Func<Order, DateTime, bool> transition, bool restoreStock, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var order = await LoadAsync(id, track: true, ct);
        if (order is null)
        {
            return NotFound();
        }

        if (!transition(order, clock.GetUtcNow().UtcDateTime))
        {
            return Error.Conflict("transition_not_allowed", $"تغییر وضعیت از «{order.Status}» مجاز نیست.");
        }

        try
        {
            await db.SaveChangesAsync(ct);
            if (restoreStock)
            {
                foreach (var item in order.Items.OrderBy(i => i.VariantId))
                {
                    await stock.IncrementAsync(item.VariantId, item.Quantity, ct);
                }
            }

            await tx.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Error.Conflict("concurrent_update", "سفارش هم‌زمان تغییر کرد. صفحه را تازه کنید.");
        }

        return ToDto(order);
    }

    private Task<Order?> LoadAsync(int id, bool track, CancellationToken ct)
    {
        var query = db.Orders.Include(o => o.Items).Include(o => o.StatusLog).AsSplitQuery();
        if (!track)
        {
            query = query.AsNoTracking();
        }

        return query.FirstOrDefaultAsync(o => o.Id == id, ct);
    }

    private AdminOrderDto ToDto(Order order) => new(
        order.Id,
        OrderMapper.ToDto(order),
        order.ClientIp,
        order.StatusLog.OrderBy(l => l.AtUtc).ThenBy(l => l.Id)
            .Select(l => new OrderStatusLogDto(l.FromStatus, l.ToStatus, l.AdminUserId, l.AtUtc)).ToList());

    private static Error NotFound() => Error.NotFound("order_not_found", "سفارش پیدا نشد.");
}
