using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shop.Application.Catalog;
using Shop.Application.Common;
using Shop.Application.Settings;
using Shop.Domain.Settings;
using Shop.Infrastructure.Persistence;

namespace Shop.Infrastructure.Settings;

/// <summary>
/// The settings, cached in memory. An admin update calls <see cref="Invalidate"/>; the age limit is only a safety net
/// (e.g. a manual database edit) because the MVP runs as a single API instance.
/// </summary>
internal sealed class StoreSettingsProvider(IServiceScopeFactory scopes, TimeProvider clock) : IStoreSettings
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(10);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private Cached? _cached;
    private int _version;

    public async Task<StoreSettingsValues> GetAsync(CancellationToken ct)
    {
        if (Fresh() is { } hit)
        {
            return hit;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (Fresh() is { } again)
            {
                return again;
            }

            var versionBefore = Volatile.Read(ref _version);
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
            var row = await db.StoreSettings.AsNoTracking().SingleOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("The store settings row is missing; the startup seeding did not run.");
            var values = row.Values;

            // An update that happened while we were loading must not be overwritten by what we read before it.
            if (versionBefore == Volatile.Read(ref _version))
            {
                _cached = new Cached(values, clock.GetUtcNow());
            }

            return values;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Invalidate()
    {
        Interlocked.Increment(ref _version);
        _cached = null;
    }

    private StoreSettingsValues? Fresh() =>
        _cached is { } cached && clock.GetUtcNow() - cached.LoadedAt < MaxAge ? cached.Values : null;

    private sealed record Cached(StoreSettingsValues Values, DateTimeOffset LoadedAt);
}

internal sealed class StoreInfoService(IStoreSettings settings) : IStoreInfoService
{
    public async Task<StoreInfoDto> GetAsync(CancellationToken ct)
    {
        var s = await settings.GetAsync(ct);
        return new StoreInfoDto(
            s.StorePhone, s.StoreAddress, s.WorkingHours, s.InstagramHandle,
            s.ShippingFlatFee, s.FreeShippingThreshold, s.CardToCardOffered, s.CashOnDeliveryEnabled);
    }
}

internal sealed class AdminSettingsService(ShopDbContext db, IStoreSettings settings, ICurrentUser currentUser, TimeProvider clock)
    : IAdminSettingsService
{
    private const int PageSize = 30;

    public async Task<AdminStoreSettingsDto> GetAsync(CancellationToken ct) =>
        ToDto(await db.StoreSettings.AsNoTracking().SingleAsync(ct));

    public async Task<Result<AdminStoreSettingsDto>> UpdateAsync(UpdateStoreSettingsRequest request, CancellationToken ct)
    {
        var (values, errors) = StoreSettingsRules.Validate(request.Settings);
        if (values is null)
        {
            return Error.Validation(errors.ToDictionary(e => e.Key, e => e.Value));
        }

        var row = await db.StoreSettings.SingleAsync(ct);
        // The version the admin loaded the form with: if someone saved in between, SaveChanges fails and nothing is lost.
        db.Entry(row).Property(x => x.RowVersion).OriginalValue = request.RowVersion;

        var now = clock.GetUtcNow().UtcDateTime;
        var changes = Diff(row.Values, values);
        if (changes.Count == 0)
        {
            return ToDto(row);
        }

        row.Apply(values, currentUser.UserId, now);
        db.SettingsAuditLogs.AddRange(changes.Select(c => SettingsAuditLog.Create(currentUser.UserId, c.Field, c.Old, c.New, now)));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Error.Conflict("concurrent_update", "تنظیمات هم‌زمان توسط شخص دیگری تغییر کرد. صفحه را تازه کنید.");
        }

        settings.Invalidate();
        return ToDto(row);
    }

    public async Task<PagedList<SettingsAuditEntryDto>> GetAuditAsync(int page, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        var total = await db.SettingsAuditLogs.CountAsync(ct);
        var items = await (
                from a in db.SettingsAuditLogs.AsNoTracking()
                join u in db.Users.AsNoTracking() on a.AdminUserId equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby a.AtUtc descending, a.Id descending
                select new SettingsAuditEntryDto(a.Id, a.AtUtc, a.AdminUserId, u.Email, a.Field, a.OldValue, a.NewValue))
            .Skip((page - 1) * PageSize).Take(PageSize)
            .ToListAsync(ct);
        return new PagedList<SettingsAuditEntryDto>(items, page, PageSize, total);
    }

    private static AdminStoreSettingsDto ToDto(StoreSetting row) =>
        new(row.Values, row.Values.CardToCardOffered, row.UpdatedAtUtc, row.UpdatedByUserId, row.RowVersion);

    /// <summary>Changed fields only. The card number is masked (last 4 digits) on both sides: the log never holds a full number.</summary>
    internal static List<(string Field, string? Old, string? New)> Diff(StoreSettingsValues before, StoreSettingsValues after)
    {
        var changes = new List<(string, string?, string?)>();

        void Check<T>(string field, T oldValue, T newValue, Func<T, string?>? show = null)
        {
            if (EqualityComparer<T>.Default.Equals(oldValue, newValue))
            {
                return;
            }

            show ??= v => v?.ToString();
            changes.Add((field, show(oldValue), show(newValue)));
        }

        Check("shippingFlatFee", before.ShippingFlatFee, after.ShippingFlatFee);
        Check("freeShippingThreshold", before.FreeShippingThreshold, after.FreeShippingThreshold);
        Check("cardToCardEnabled", before.CardToCardEnabled, after.CardToCardEnabled, v => v ? "true" : "false");
        Check("cashOnDeliveryEnabled", before.CashOnDeliveryEnabled, after.CashOnDeliveryEnabled, v => v ? "true" : "false");
        Check("cardNumber", before.CardNumber, after.CardNumber, CardNumbers.Mask);
        Check("cardHolderName", before.CardHolderName, after.CardHolderName);
        Check("bankName", before.BankName, after.BankName);
        Check("maxQuantityPerLine", before.MaxQuantityPerLine, after.MaxQuantityPerLine);
        Check("storePhone", before.StorePhone, after.StorePhone);
        Check("storeAddress", before.StoreAddress, after.StoreAddress);
        Check("workingHours", before.WorkingHours, after.WorkingHours);
        Check("instagramHandle", before.InstagramHandle, after.InstagramHandle);
        return changes;
    }
}
