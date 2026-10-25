namespace Shop.Domain.Settings;

/// <summary>
/// The store's editable settings: exactly one row (Id = 1), typed columns. A single typed record was chosen over a
/// key/value table because every field has its own type and validation rule, a row version gives optimistic
/// concurrency for free, and orders/audit can diff plain properties. Secrets and limits stay in configuration.
/// </summary>
public sealed class StoreSetting
{
    public const int SingletonId = 1;

    private StoreSetting()
    {
        CardNumber = CardHolderName = BankName = StorePhone = StoreAddress = WorkingHours = InstagramHandle = string.Empty;
    }

    public int Id { get; private set; } = SingletonId;
    public long ShippingFlatFee { get; private set; }
    public long? FreeShippingThreshold { get; private set; }
    public bool CardToCardEnabled { get; private set; }
    public bool CashOnDeliveryEnabled { get; private set; }
    public string CardNumber { get; private set; }
    public string CardHolderName { get; private set; }
    public string BankName { get; private set; }
    public int MaxQuantityPerLine { get; private set; }
    public string StorePhone { get; private set; }
    public string StoreAddress { get; private set; }
    public string WorkingHours { get; private set; }
    public string InstagramHandle { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public StoreSettingsValues Values => new(
        ShippingFlatFee, FreeShippingThreshold, CardToCardEnabled, CashOnDeliveryEnabled, CardNumber, CardHolderName, BankName,
        MaxQuantityPerLine, StorePhone, StoreAddress, WorkingHours, InstagramHandle);

    public static StoreSetting Create(StoreSettingsValues values, DateTime utcNow)
    {
        var setting = new StoreSetting();
        setting.Apply(values, null, utcNow);
        return setting;
    }

    public void Apply(StoreSettingsValues values, Guid? userId, DateTime utcNow)
    {
        ShippingFlatFee = values.ShippingFlatFee;
        FreeShippingThreshold = values.FreeShippingThreshold;
        CardToCardEnabled = values.CardToCardEnabled;
        CashOnDeliveryEnabled = values.CashOnDeliveryEnabled;
        CardNumber = values.CardNumber;
        CardHolderName = values.CardHolderName;
        BankName = values.BankName;
        MaxQuantityPerLine = values.MaxQuantityPerLine;
        StorePhone = values.StorePhone;
        StoreAddress = values.StoreAddress;
        WorkingHours = values.WorkingHours;
        InstagramHandle = values.InstagramHandle;
        UpdatedAtUtc = utcNow;
        UpdatedByUserId = userId;
    }
}

/// <summary>One changed field of one settings update. Card numbers are stored masked (last 4 digits only).</summary>
public sealed class SettingsAuditLog
{
    public const int FieldMaxLength = 50;
    public const int ValueMaxLength = 500;

    private SettingsAuditLog()
    {
        Field = string.Empty;
    }

    public int Id { get; private set; }
    public DateTime AtUtc { get; private set; }
    public Guid? AdminUserId { get; private set; }
    public string Field { get; private set; }
    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }

    public static SettingsAuditLog Create(Guid? adminUserId, string field, string? oldValue, string? newValue, DateTime utcNow) => new()
    {
        AtUtc = utcNow,
        AdminUserId = adminUserId,
        Field = field,
        OldValue = Clip(oldValue),
        NewValue = Clip(newValue),
    };

    private static string? Clip(string? value) => value is { Length: > ValueMaxLength } ? value[..ValueMaxLength] : value;
}
