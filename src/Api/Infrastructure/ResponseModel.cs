namespace Shop.Api.Infrastructure;

/// <summary>
/// The envelope every API response body is wrapped in, success or error. HTTP status codes stay real (401, 404,
/// 409, ...); this only standardizes the body shape across minimal-API endpoints and controllers alike.
/// </summary>
public sealed class ResponseModel : ResponseModel<string?>
{
}

public class ResponseModel<T>
{
    public T? Result { get; set; }

    public string? ErrorMessage { get; set; }

    public string? SuccessMessage { get; set; }

    public string? Message => IsSuccess ? SuccessMessage : ErrorMessage;

    public string? InfoMessage { get; set; }

    /// <summary>Stable machine code for the error (e.g. "product_not_found"), when there is one.</summary>
    public string? ErrorCode { get; set; }

    /// <summary>Field-level validation errors, when the error is a validation failure.</summary>
    public IDictionary<string, string[]>? ValidationErrors { get; set; }

    public bool IsSuccess => string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsError => !string.IsNullOrWhiteSpace(ErrorMessage);
}
