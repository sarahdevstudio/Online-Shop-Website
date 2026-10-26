namespace Shop.Api.Infrastructure;

/// <summary>Persian titles for error responses: no English text may reach a visitor or an admin through an API error.</summary>
internal static class PersianProblemText
{
    public const string Validation = "یک یا چند مقدار نامعتبر است.";
    public const string Unauthorized = "ورود لازم است.";
    public const string Forbidden = "اجازهٔ دسترسی ندارید.";
    public const string NotFound = "پیدا نشد.";
    public const string Conflict = "تداخل در اطلاعات.";
    public const string TooManyRequests = "تعداد درخواست‌ها زیاد است؛ کمی بعد دوباره تلاش کنید.";
    public const string BadRequest = "درخواست نامعتبر است.";
    public const string Unexpected = "خطای پیش‌بینی‌نشده‌ای رخ داد.";
    public const string RequestBodyRequired = "بدنهٔ درخواست لازم است.";
    public const string StorefrontKeyRequired = "کلید فروشگاه معتبر نیست.";
    public const string AntiforgeryInvalid = "توکن امنیتی درخواست نامعتبر یا ناموجود است. صفحه را تازه کنید.";
    public const string MalformedRequest = "درخواست ارسال‌شده قابل پردازش نیست.";
    public const string ConcurrentUpdate = "اطلاعات هم‌زمان توسط شخص دیگری تغییر کرد. صفحه را تازه کنید و دوباره تلاش کنید.";

    public static bool HasPersian(string? text) => text is not null && text.Any(c => c is >= '\u0600' and <= '\u06FF');

    /// <summary>Title for a status code when the framework (not our code) produced the response, e.g. an unknown route.</summary>
    public static string? ForStatus(int? status) => status switch
    {
        400 => BadRequest,
        401 => Unauthorized,
        403 => Forbidden,
        404 => NotFound,
        405 => "روش درخواست مجاز نیست.",
        409 => Conflict,
        413 => "حجم درخواست بیش از حد مجاز است.",
        415 => "نوع محتوای درخواست پشتیبانی نمی‌شود.",
        429 => TooManyRequests,
        >= 500 => Unexpected,
        _ => null,
    };
}
