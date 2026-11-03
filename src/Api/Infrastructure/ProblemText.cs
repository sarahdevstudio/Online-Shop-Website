using Shop.Application.Translations;

namespace Shop.Api.Infrastructure;

/// <summary>
/// Titles for error responses, read from the translation table at the moment of the response so an edit applies at once. No
/// English text may reach a visitor or an admin through an API error (docs/11 B.3).
/// </summary>
internal static class ProblemText
{
    public static string Validation => Translate.Get("api.error.validation-failed");
    public static string Unauthorized => Translate.Get("api.error.unauthorized");
    public static string Forbidden => Translate.Get("api.error.forbidden");
    public static string NotFound => Translate.Get("api.error.not-found");
    public static string Conflict => Translate.Get("api.error.conflict");
    public static string TooManyRequests => Translate.Get("api.error.too-many-requests");
    public static string BadRequest => Translate.Get("api.error.bad-request");
    public static string Unexpected => Translate.Get("api.error.unexpected");
    public static string RequestBodyRequired => Translate.Get("api.error.request-body-required");
    public static string StorefrontKeyRequired => Translate.Get("api.error.storefront-key-required");
    public static string AntiforgeryInvalid => Translate.Get("api.error.antiforgery-invalid");
    public static string MalformedRequest => Translate.Get("api.error.malformed-request");
    public static string ConcurrentUpdate => Translate.Get("api.error.concurrent-update");

    public static bool HasPersian(string? text) => text is not null && text.Any(c => c is >= '؀' and <= 'ۿ');

    /// <summary>Title for a status code when the framework (not our code) produced the response, e.g. an unknown route.</summary>
    public static string? ForStatus(int? status) => status switch
    {
        400 => BadRequest,
        401 => Unauthorized,
        403 => Forbidden,
        404 => NotFound,
        405 => Translate.Get("api.error.method-not-allowed"),
        409 => Conflict,
        413 => Translate.Get("api.error.payload-too-large"),
        415 => Translate.Get("api.error.unsupported-media-type"),
        429 => TooManyRequests,
        >= 500 => Unexpected,
        _ => null,
    };
}
