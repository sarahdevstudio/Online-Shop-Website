using FluentValidation;
using Shop.Domain.Common;
using Shop.Domain.Orders;

namespace Shop.Application.Orders;

public sealed class AddCartItemRequestValidator : AbstractValidator<AddCartItemRequest>
{
    public AddCartItemRequestValidator()
    {
        RuleFor(x => x.VariantId).GreaterThan(0);
        RuleFor(x => x.Quantity).InclusiveBetween(1, 1000);
    }
}

public sealed class UpdateCartItemRequestValidator : AbstractValidator<UpdateCartItemRequest>
{
    public UpdateCartItemRequestValidator()
    {
        RuleFor(x => x.Quantity).InclusiveBetween(0, 1000);
    }
}

public sealed class PlaceOrderRequestValidator : AbstractValidator<PlaceOrderRequest>
{
    public PlaceOrderRequestValidator()
    {
        RuleFor(x => x.FullName).Must(v => LengthBetween(v, 3, Order.NameMaxLength)).WithMessage("نام و نام خانوادگی را وارد کنید.");
        RuleFor(x => x.Mobile).Must(v => IranianMobile.TryNormalize(v, out _)).WithMessage("شماره موبایل معتبر نیست (مثال: ۰۹۱۲۳۴۵۶۷۸۹).");
        RuleFor(x => x.Province).Must(v => LengthBetween(v, 2, Order.ProvinceMaxLength)).WithMessage("استان را وارد کنید.");
        RuleFor(x => x.City).Must(v => LengthBetween(v, 2, Order.CityMaxLength)).WithMessage("شهر را وارد کنید.");
        RuleFor(x => x.Address).Must(v => LengthBetween(v, 10, Order.AddressMaxLength)).WithMessage("نشانی کامل را وارد کنید (حداقل ۱۰ نویسه).");
        RuleFor(x => x.PostalCode).Must(v => PostalCodes.TryNormalize(v, out _)).WithMessage("کد پستی باید ۱۰ رقم باشد.");
        RuleFor(x => x.Note).Must(v => v is null || PersianText.NormalizeLetters(v).Length <= Order.NoteMaxLength)
            .WithMessage($"توضیحات حداکثر {Order.NoteMaxLength} نویسه باشد.");
        RuleFor(x => x.PaymentMethod).IsInEnum().WithMessage("روش پرداخت را انتخاب کنید.");
        RuleFor(x => x.ExpectedTotal).GreaterThanOrEqualTo(0);
    }

    private static bool LengthBetween(string? value, int min, int max) =>
        PersianText.NormalizeLetters(value).Length is var n && n >= min && n <= max;
}

public sealed class TransferReferenceRequestValidator : AbstractValidator<TransferReferenceRequest>
{
    public TransferReferenceRequestValidator()
    {
        RuleFor(x => x.Tracking).Must(v => PersianText.Normalize(Digits.ToLatin(v)).Length is var n && n >= 4 && n <= Order.TrackingMaxLength)
            .WithMessage("شماره پیگیری را وارد کنید.");
        RuleFor(x => x.Time).Must(v => PersianText.Normalize(Digits.ToLatin(v)).Length is var n && n >= 1 && n <= Order.TransferTimeMaxLength)
            .WithMessage("زمان انتقال را وارد کنید.");
        RuleFor(x => x.CardLast4).Must(v => Digits.ToLatin(v) is { Length: 4 } d && d.All(char.IsAsciiDigit))
            .WithMessage("۴ رقم آخر کارت را وارد کنید.");
    }
}

public static class PostalCodes
{
    public static bool TryNormalize(string? input, out string postalCode)
    {
        postalCode = Digits.ToLatin(input);
        return postalCode.Length == 10 && postalCode.All(char.IsAsciiDigit);
    }
}
