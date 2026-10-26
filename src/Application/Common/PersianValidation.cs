using System.Globalization;
using FluentValidation;

namespace Shop.Application.Common;

/// <summary>
/// FluentValidation's built-in messages ("'Email' must not be empty") would show English text and English property names.
/// This switches them to Persian and names every request property in Persian, once at startup.
/// </summary>
public static class PersianValidation
{
    private const string UnknownLabel = "این مقدار";

    private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
    {
        ["Address"] = "نشانی",
        ["CardLast"] = "۴ رقم آخر کارت",
        ["CategoryId"] = "دسته",
        ["City"] = "شهر",
        ["CompareAtPrice"] = "قیمت قبل از تخفیف",
        ["CurrentPassword"] = "رمز عبور فعلی",
        ["Description"] = "توضیحات",
        ["Email"] = "ایمیل",
        ["ExpectedTotal"] = "مبلغ کل",
        ["FullName"] = "نام و نام خانوادگی",
        ["ImageIds"] = "تصاویر",
        ["Mobile"] = "شماره موبایل",
        ["Name"] = "نام",
        ["NewPassword"] = "رمز عبور جدید",
        ["Note"] = "توضیحات",
        ["Password"] = "رمز عبور",
        ["PaymentMethod"] = "روش پرداخت",
        ["Phone"] = "تلفن",
        ["PostalCode"] = "کد پستی",
        ["Price"] = "قیمت",
        ["Province"] = "استان",
        ["Quantity"] = "تعداد",
        ["RowVersion"] = "نسخهٔ اطلاعات",
        ["Sku"] = "کد کالا",
        ["Slug"] = "شناسهٔ آدرس",
        ["Status"] = "وضعیت",
        ["Stock"] = "موجودی",
        ["Time"] = "زمان",
        ["Tracking"] = "شماره پیگیری",
        ["Username"] = "نام کاربری",
        ["Value"] = "مقدار",
        ["Values"] = "مقادیر",
        ["VariantId"] = "تنوع",
        ["Variants"] = "تنوع‌ها",
    };

    public static string Label(string? property) => property is not null && Labels.TryGetValue(property, out var label) ? label : UnknownLabel;

    public static void Configure()
    {
        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("fa");
        ValidatorOptions.Global.DisplayNameResolver = (_, member, _) => Label(member?.Name);
    }
}
