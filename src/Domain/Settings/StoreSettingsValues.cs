using Shop.Domain.Common;
using Shop.Domain.Orders;

namespace Shop.Domain.Settings;

/// <summary>
/// The admin-editable store settings, as plain values (already normalized when they come from
/// <see cref="StoreSettingsRules.Validate"/>). Money is whole Toman.
/// </summary>
public sealed record StoreSettingsValues(
    long ShippingFlatFee,
    long? FreeShippingThreshold,
    bool CardToCardEnabled,
    bool CashOnDeliveryEnabled,
    string CardNumber,
    string CardHolderName,
    string BankName,
    int MaxQuantityPerLine,
    string StorePhone,
    string StoreAddress,
    string WorkingHours,
    string InstagramHandle)
{
    /// <summary>
    /// Card-to-card is only offered to customers when it is switched on AND the card details are real. A freshly seeded
    /// store (placeholder or empty card) therefore simply doesn't show it.
    /// </summary>
    public bool CardToCardOffered =>
        CardToCardEnabled && CardNumbers.IsUsable(CardNumber) && StoreSettingsRules.IsUsableHolder(CardHolderName);

    public bool IsOffered(PaymentMethod method) => method switch
    {
        PaymentMethod.CardToCard => CardToCardOffered,
        PaymentMethod.CashOnDelivery => CashOnDeliveryEnabled,
        _ => false,
    };
}

public static class StoreSettingsRules
{
    public const long MaxAmount = 1_000_000_000_000;
    public const int MaxQuantityLimit = 100;
    public const int HolderMaxLength = 100;
    public const int BankMaxLength = 50;
    public const int PhoneMaxLength = 30;
    public const int AddressMaxLength = 300;
    public const int HoursMaxLength = 100;
    public const int InstagramMaxLength = 30;

    public static bool IsUsableHolder(string? holder) =>
        !string.IsNullOrWhiteSpace(holder) && PersianText.Normalize(holder) is { Length: >= 2 } normalized
        && normalized != PersianText.Normalize(CardNumbers.PlaceholderHolder);

    /// <summary>
    /// Normalizes the input and checks every rule. Errors are keyed by camelCase field name. A card number is optional
    /// while card-to-card is off, but must be valid whenever it is given.
    /// </summary>
    public static (StoreSettingsValues? Values, IReadOnlyDictionary<string, string[]> Errors) Validate(StoreSettingsValues input)
    {
        var errors = new Dictionary<string, string[]>();

        if (input.ShippingFlatFee is < 0 or > MaxAmount)
        {
            errors["shippingFlatFee"] = ["هزینه ارسال باید عدد صحیح و نامنفی باشد."];
        }

        if (input.FreeShippingThreshold is < 0 or > MaxAmount)
        {
            errors["freeShippingThreshold"] = ["آستانه ارسال رایگان باید خالی یا عدد صحیح و نامنفی باشد."];
        }

        if (input.MaxQuantityPerLine is < 1 or > MaxQuantityLimit)
        {
            errors["maxQuantityPerLine"] = [$"حداکثر تعداد در هر ردیف باید بین ۱ تا {MaxQuantityLimit} باشد."];
        }

        var cardInput = (input.CardNumber ?? string.Empty).Trim();
        var cardDigits = cardInput.Length == 0 ? string.Empty : CardNumbers.Normalize(cardInput);
        if (cardDigits is null || (cardDigits.Length > 0 && !CardNumbers.PassesLuhn(cardDigits)))
        {
            errors["cardNumber"] = ["شماره کارت باید ۱۶ رقم و معتبر باشد."];
        }
        else if (cardDigits.Length > 0 && CardNumbers.IsPlaceholder(cardDigits))
        {
            errors["cardNumber"] = ["این شماره کارت نمونه است؛ شماره کارت واقعی را وارد کنید."];
        }

        var holder = PersianText.Normalize(input.CardHolderName);
        if (holder.Length > HolderMaxLength)
        {
            errors["cardHolderName"] = [$"نام صاحب کارت حداکثر {HolderMaxLength} نویسه باشد."];
        }

        var bank = PersianText.Normalize(input.BankName);
        if (bank.Length > BankMaxLength)
        {
            errors["bankName"] = [$"نام بانک حداکثر {BankMaxLength} نویسه باشد."];
        }

        if (input.CardToCardEnabled)
        {
            if (!errors.ContainsKey("cardNumber") && string.IsNullOrEmpty(cardDigits))
            {
                errors["cardNumber"] = ["برای فعال‌سازی کارت‌به‌کارت، شماره کارت لازم است."];
            }

            if (!errors.ContainsKey("cardHolderName") && !IsUsableHolder(holder))
            {
                errors["cardHolderName"] = ["برای فعال‌سازی کارت‌به‌کارت، نام صاحب کارت لازم است (نه نام نمونه)."];
            }
        }

        if (!input.CardToCardEnabled && !input.CashOnDeliveryEnabled)
        {
            errors["paymentMethods"] = ["حداقل یکی از روش‌های پرداخت باید فعال باشد."];
        }

        var phone = Digits.ToLatin(PersianText.Normalize(input.StorePhone));
        var address = PersianText.NormalizeLetters(input.StoreAddress);
        var hours = PersianText.Normalize(input.WorkingHours);
        var instagram = NormalizeInstagram(input.InstagramHandle);
        if (phone.Length > PhoneMaxLength)
        {
            errors["storePhone"] = [$"شماره تماس حداکثر {PhoneMaxLength} نویسه باشد."];
        }

        if (address.Length > AddressMaxLength)
        {
            errors["storeAddress"] = [$"نشانی حداکثر {AddressMaxLength} نویسه باشد."];
        }

        if (hours.Length > HoursMaxLength)
        {
            errors["workingHours"] = [$"ساعات کاری حداکثر {HoursMaxLength} نویسه باشد."];
        }

        if (instagram is null)
        {
            errors["instagramHandle"] = ["شناسه اینستاگرام فقط شامل حروف انگلیسی، عدد، نقطه و زیرخط باشد (مثال: myshop)."];
        }

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        return (new StoreSettingsValues(
            input.ShippingFlatFee, input.FreeShippingThreshold, input.CardToCardEnabled, input.CashOnDeliveryEnabled,
            cardDigits ?? string.Empty, holder, bank, input.MaxQuantityPerLine, phone, address, hours, instagram!), errors);
    }

    /// <summary>"@shop", " shop ", "https://instagram.com/shop/" → "shop"; empty stays empty; anything odd → null.</summary>
    public static string? NormalizeInstagram(string? input)
    {
        var value = (input ?? string.Empty).Trim();
        foreach (var prefix in new[] { "https://www.instagram.com/", "https://instagram.com/", "http://www.instagram.com/", "http://instagram.com/", "instagram.com/" })
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                value = value[prefix.Length..];
                break;
            }
        }

        value = value.TrimStart('@').TrimEnd('/');
        if (value.Length == 0)
        {
            return string.Empty;
        }

        return value.Length <= InstagramMaxLength && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_') ? value : null;
    }
}
