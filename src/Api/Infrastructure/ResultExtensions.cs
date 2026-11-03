using Shop.Application.Common;
using Shop.Application.Translations;

namespace Shop.Api.Infrastructure;

internal static class ResultExtensions
{
    public static IResult ToProblem(this Error error)
    {
        var message = Translate.Resolve(error.MessageKey);
        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };
        if (error.Extensions is not null)
        {
            foreach (var (key, value) in error.Extensions)
            {
                extensions[key] = value;
            }
        }

        return error.Type switch
        {
            ErrorType.Validation => Results.ValidationProblem(
                Messages.Resolve(error.ValidationErrors),
                title: message,
                extensions: extensions),
            ErrorType.Unauthorized => Results.Problem(message, statusCode: StatusCodes.Status401Unauthorized, title: ProblemText.Unauthorized, extensions: extensions),
            ErrorType.Forbidden => Results.Problem(message, statusCode: StatusCodes.Status403Forbidden, title: ProblemText.Forbidden, extensions: extensions),
            ErrorType.NotFound => Results.Problem(message, statusCode: StatusCodes.Status404NotFound, title: ProblemText.NotFound, extensions: extensions),
            ErrorType.Conflict => Results.Problem(message, statusCode: StatusCodes.Status409Conflict, title: ProblemText.Conflict, extensions: extensions),
            ErrorType.TooManyRequests => Results.Problem(message, statusCode: StatusCodes.Status429TooManyRequests, title: ProblemText.TooManyRequests, extensions: extensions),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: ProblemText.Unexpected),
        };
    }

    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : result.Error!.ToProblem();

    public static IResult ToHttpResult(this Result result, Func<IResult> onSuccess) =>
        result.IsSuccess ? onSuccess() : result.Error!.ToProblem();
}
/// <summary>Turns the message keys carried in validation dictionaries into text, once, at the API boundary.</summary>
internal static class Messages
{
    public static Dictionary<string, string[]> Resolve(IEnumerable<KeyValuePair<string, string[]>>? errors) =>
        (errors ?? []).ToDictionary(e => e.Key, e => e.Value.Select(Translate.Resolve).ToArray(), StringComparer.Ordinal);
}
