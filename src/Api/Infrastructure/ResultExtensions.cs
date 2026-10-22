using Shop.Application.Common;

namespace Shop.Api.Infrastructure;

internal static class ResultExtensions
{
    public static IResult ToProblem(this Error error)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };

        return error.Type switch
        {
            ErrorType.Validation => Results.ValidationProblem(
                error.ValidationErrors ?? new Dictionary<string, string[]>(),
                title: error.Message,
                extensions: extensions),
            ErrorType.Unauthorized => Results.Problem(error.Message, statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized", extensions: extensions),
            ErrorType.Forbidden => Results.Problem(error.Message, statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", extensions: extensions),
            ErrorType.NotFound => Results.Problem(error.Message, statusCode: StatusCodes.Status404NotFound, title: "Not found", extensions: extensions),
            ErrorType.Conflict => Results.Problem(error.Message, statusCode: StatusCodes.Status409Conflict, title: "Conflict", extensions: extensions),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "An unexpected error occurred."),
        };
    }

    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : result.Error!.ToProblem();

    public static IResult ToHttpResult(this Result result, Func<IResult> onSuccess) =>
        result.IsSuccess ? onSuccess() : result.Error!.ToProblem();
}
