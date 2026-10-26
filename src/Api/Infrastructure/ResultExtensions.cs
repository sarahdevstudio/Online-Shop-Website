using Shop.Application.Common;

namespace Shop.Api.Infrastructure;

internal static class ResultExtensions
{
    public static IResult ToProblem(this Error error)
    {
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
                error.ValidationErrors ?? new Dictionary<string, string[]>(),
                title: error.Message,
                extensions: extensions),
            ErrorType.Unauthorized => Results.Problem(error.Message, statusCode: StatusCodes.Status401Unauthorized, title: PersianProblemText.Unauthorized, extensions: extensions),
            ErrorType.Forbidden => Results.Problem(error.Message, statusCode: StatusCodes.Status403Forbidden, title: PersianProblemText.Forbidden, extensions: extensions),
            ErrorType.NotFound => Results.Problem(error.Message, statusCode: StatusCodes.Status404NotFound, title: PersianProblemText.NotFound, extensions: extensions),
            ErrorType.Conflict => Results.Problem(error.Message, statusCode: StatusCodes.Status409Conflict, title: PersianProblemText.Conflict, extensions: extensions),
            ErrorType.TooManyRequests => Results.Problem(error.Message, statusCode: StatusCodes.Status429TooManyRequests, title: PersianProblemText.TooManyRequests, extensions: extensions),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: PersianProblemText.Unexpected),
        };
    }

    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : result.Error!.ToProblem();

    public static IResult ToHttpResult(this Result result, Func<IResult> onSuccess) =>
        result.IsSuccess ? onSuccess() : result.Error!.ToProblem();
}
