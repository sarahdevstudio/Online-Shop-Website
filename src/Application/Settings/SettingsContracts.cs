using Shop.Application.Catalog;
using Shop.Domain.Settings;

namespace Shop.Application.Settings;

/// <summary>
/// First values for the store settings (<c>StoreDefaults</c> in appsettings.json). Written to the database only when the
/// settings row does not exist yet; afterwards the admin edits the database and these are ignored.
/// </summary>
public sealed class StoreDefaultsOptions
{
    public const string SectionName = "StoreDefaults";

    public long ShippingFlatFee { get; set; } = 50_000;
    public long? FreeShippingThreshold { get; set; }
    public bool CardToCardEnabled { get; set; } = true;
    public bool CashOnDeliveryEnabled { get; set; } = true;
    public string CardNumber { get; set; } = string.Empty;
    public string CardHolderName { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public int MaxQuantityPerLine { get; set; } = 10;
    public string StorePhone { get; set; } = string.Empty;
    public string StoreAddress { get; set; } = string.Empty;
    public string WorkingHours { get; set; } = string.Empty;
    public string InstagramHandle { get; set; } = string.Empty;

    public StoreSettingsValues ToValues() => new(
        ShippingFlatFee, FreeShippingThreshold, CardToCardEnabled, CashOnDeliveryEnabled, CardNumber ?? string.Empty,
        CardHolderName ?? string.Empty, BankName ?? string.Empty, MaxQuantityPerLine, StorePhone ?? string.Empty,
        StoreAddress ?? string.Empty, WorkingHours ?? string.Empty, InstagramHandle ?? string.Empty);
}

/// <summary>Read access to the settings for the rest of the app. Cached in memory; an admin update invalidates the cache.</summary>
public interface IStoreSettings
{
    Task<StoreSettingsValues> GetAsync(CancellationToken ct);

    void Invalidate();
}

public sealed record AdminStoreSettingsDto(
    StoreSettingsValues Settings, bool CardToCardOffered, DateTime UpdatedAtUtc, Guid? UpdatedByUserId, byte[] RowVersion);

/// <summary>The full settings plus the row version read with them; sending it back makes a stale save fail with 409.</summary>
/// <summary>
/// <see cref="CurrentPassword"/> is the admin's own password: required (step-up re-authentication) when the change touches
/// the card number, holder or bank, or switches a payment method on or off.
/// </summary>
public sealed record UpdateStoreSettingsRequest(StoreSettingsValues Settings, byte[] RowVersion, string? CurrentPassword = null);

public sealed record SettingsAuditEntryDto(
    int Id, DateTime AtUtc, Guid? AdminUserId, string? AdminEmail, string Field, string? OldValue, string? NewValue);

/// <summary>Only what customers may see. No card details: those appear on the customer's own order page.</summary>
public sealed record StoreInfoDto(
    string StorePhone, string StoreAddress, string WorkingHours, string InstagramHandle,
    long ShippingFlatFee, long? FreeShippingThreshold, bool CardToCard, bool CashOnDelivery);

public interface IAdminSettingsService
{
    Task<AdminStoreSettingsDto> GetAsync(CancellationToken ct);

    Task<Common.Result<AdminStoreSettingsDto>> UpdateAsync(UpdateStoreSettingsRequest request, CancellationToken ct);

    Task<PagedList<SettingsAuditEntryDto>> GetAuditAsync(int page, CancellationToken ct);
}

public interface IStoreInfoService
{
    Task<StoreInfoDto> GetAsync(CancellationToken ct);
}
