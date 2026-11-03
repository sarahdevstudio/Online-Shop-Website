namespace Shop.Application.Common;

public enum ErrorType
{
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    TooManyRequests,
}

public sealed record Error(
    ErrorType Type,
    string Code,
    string MessageKey,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null,
    IReadOnlyDictionary<string, object?>? Extensions = null)
{
    public static Error Validation(IReadOnlyDictionary<string, string[]> errors) =>
        new(ErrorType.Validation, "validation_failed", "api.error.validation-failed", errors);

    public static Error Unauthorized(string code, string messageKey) => new(ErrorType.Unauthorized, code, messageKey);
    public static Error Forbidden(string code, string messageKey) => new(ErrorType.Forbidden, code, messageKey);
    public static Error NotFound(string code, string messageKey) => new(ErrorType.NotFound, code, messageKey);
    public static Error Conflict(string code, string messageKey) => new(ErrorType.Conflict, code, messageKey);
    public static Error TooManyRequests(string code, string messageKey) => new(ErrorType.TooManyRequests, code, messageKey);

    /// <summary>A conflict that carries machine-readable details (e.g. which cart lines are short on stock).</summary>
    public static Error Conflict(string code, string messageKey, IReadOnlyDictionary<string, object?> extensions) =>
        new(ErrorType.Conflict, code, messageKey, null, extensions);
}

public class Result
{
    protected Result(Error? error) => Error = error;

    public Error? Error { get; }
    public bool IsSuccess => Error is null;

    public static Result Success() => new(null);
    public static Result Failure(Error error) => new(error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value) : base(null) => _value = value;
    private Result(Error error) : base(error) { }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot read the value of a failed result.");

    public static Result<T> Success(T value) => new(value);
    public static new Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(Error error) => Failure(error);
    public static implicit operator Result<T>(T value) => Success(value);
}
