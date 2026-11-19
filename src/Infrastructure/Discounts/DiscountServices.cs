using Microsoft.EntityFrameworkCore;
using Shop.Application.Common;
using Shop.Application.Discounts;
using Shop.Domain.Common;
using Shop.Domain.Discounts;
using Shop.Domain.Orders;
using Shop.Infrastructure.Persistence;

namespace Shop.Infrastructure.Discounts;

internal sealed class AdminDiscountService(ShopDbContext db, ICurrentUser currentUser, TimeProvider clock) : IAdminDiscountService
{
    private const string CodeIndex = "UX_DiscountCodes_Code";

    public async Task<IReadOnlyList<AdminDiscountListItemDto>> ListAsync(CancellationToken ct) =>
        await (
                from d in db.DiscountCodes.AsNoTracking()
                orderby d.CreatedAtUtc descending, d.Id descending
                select new AdminDiscountListItemDto(
                    d.Id, d.Code, d.Type, d.Value, d.MinSubtotal, d.UsageLimit,
                    db.Orders.Count(o => o.DiscountCode == d.Code && o.Status != OrderStatus.Cancelled),
                    d.PerMobileLimit, d.ExpiresAtUtc, d.IsActive))
            .ToListAsync(ct);

    public async Task<Result<AdminDiscountDto>> GetAsync(int id, CancellationToken ct)
    {
        var dto = await LoadAsync(id, ct);
        return dto is null ? NotFound() : dto;
    }

    public async Task<Result<AdminDiscountDto>> CreateAsync(CreateDiscountCodeRequest request, CancellationToken ct)
    {
        var errors = DiscountCode.Validate(request.Code, request.Type, request.Value, request.MinSubtotal, request.UsageLimit, request.PerMobileLimit);
        if (errors.Count > 0)
        {
            return Error.Validation(errors);
        }

        var normalized = DiscountCode.Normalize(request.Code);
        if (await db.DiscountCodes.AnyAsync(d => d.Code == normalized, ct))
        {
            return CodeTaken;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var discount = DiscountCode.Create(
            request.Code, request.Type, request.Value, request.MinSubtotal, request.UsageLimit, request.PerMobileLimit,
            request.ExpiresAtUtc, request.IsActive, now);
        db.DiscountCodes.Add(discount);
        db.DiscountAuditLogs.Add(DiscountAuditLog.Create(discount.Code, DiscountAuditLog.Created, currentUser.UserId, now));

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex, CodeIndex))
        {
            return CodeTaken;
        }

        return (await LoadAsync(discount.Id, ct))!;
    }

    public async Task<Result<AdminDiscountDto>> UpdateAsync(int id, UpdateDiscountCodeRequest request, CancellationToken ct)
    {
        var discount = await db.DiscountCodes.SingleOrDefaultAsync(d => d.Id == id, ct);
        if (discount is null)
        {
            return NotFound();
        }

        var errors = DiscountCode.Validate(discount.Code, request.Type, request.Value, request.MinSubtotal, request.UsageLimit, request.PerMobileLimit);
        if (errors.Count > 0)
        {
            return Error.Validation(errors);
        }

        // The version the editor loaded: if someone saved meanwhile, nothing is overwritten and the editor gets a 409.
        db.Entry(discount).Property(d => d.RowVersion).OriginalValue = request.RowVersion;
        var now = clock.GetUtcNow().UtcDateTime;
        discount.Apply(request.Type, request.Value, request.MinSubtotal, request.UsageLimit, request.PerMobileLimit, request.ExpiresAtUtc, request.IsActive, now);
        db.DiscountAuditLogs.Add(DiscountAuditLog.Create(discount.Code, DiscountAuditLog.Updated, currentUser.UserId, now));

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict();
        }

        return (await LoadAsync(id, ct))!;
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct)
    {
        var discount = await db.DiscountCodes.SingleOrDefaultAsync(d => d.Id == id, ct);
        if (discount is null)
        {
            return Result.Failure(NotFound());
        }

        // The code stays on any order that already used it (a snapshot); only future use is cut off.
        db.DiscountCodes.Remove(discount);
        db.DiscountAuditLogs.Add(DiscountAuditLog.Create(discount.Code, DiscountAuditLog.Deleted, currentUser.UserId, clock.GetUtcNow().UtcDateTime));
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<DiscountAuditEntryDto>>> AuditAsync(int id, CancellationToken ct)
    {
        var code = await db.DiscountCodes.AsNoTracking().Where(d => d.Id == id).Select(d => d.Code).SingleOrDefaultAsync(ct);
        if (code is null)
        {
            return NotFound();
        }

        IReadOnlyList<DiscountAuditEntryDto> rows = await (
                from a in db.DiscountAuditLogs.AsNoTracking()
                where a.Code == code
                join u in db.Users.AsNoTracking() on a.AdminUserId equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby a.AtUtc descending, a.Id descending
                select new DiscountAuditEntryDto(a.AtUtc, a.Action, u.UserName))
            .Take(30)
            .ToListAsync(ct);
        return Result<IReadOnlyList<DiscountAuditEntryDto>>.Success(rows);
    }

    private async Task<AdminDiscountDto?> LoadAsync(int id, CancellationToken ct) =>
        await (
                from d in db.DiscountCodes.AsNoTracking()
                where d.Id == id
                select new AdminDiscountDto(
                    d.Id, d.Code, d.Type, d.Value, d.MinSubtotal, d.UsageLimit,
                    db.Orders.Count(o => o.DiscountCode == d.Code && o.Status != OrderStatus.Cancelled),
                    d.PerMobileLimit, d.ExpiresAtUtc, d.IsActive, d.CreatedAtUtc, d.RowVersion))
            .SingleOrDefaultAsync(ct);

    private static Error NotFound() => Error.NotFound("discount_not_found", "api.error.discount-not-found");

    private static Error Conflict() => Error.Conflict("concurrent_update", "api.error.discount-concurrent-update");

    private static Error CodeTaken => Error.Conflict("code_taken", "api.error.discount-code-taken");
}
