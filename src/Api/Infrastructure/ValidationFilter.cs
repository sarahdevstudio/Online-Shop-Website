using FluentValidation;

namespace Shop.Api.Infrastructure;

/// <summary>Validates the bound request body with FluentValidation before the handler runs.</summary>
internal sealed class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<T>().FirstOrDefault();
        if (request is null)
        {
            return Results.Problem(
                PersianProblemText.RequestBodyRequired,
                statusCode: StatusCodes.Status400BadRequest,
                title: PersianProblemText.BadRequest);
        }

        var validator = context.HttpContext.RequestServices.GetRequiredService<IValidator<T>>();
        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
        if (!result.IsValid)
        {
            return Results.ValidationProblem(result.ToDictionary(), title: PersianProblemText.Validation);
        }

        return await next(context);
    }
}

internal static class ValidationFilterExtensions
{
    public static RouteHandlerBuilder WithValidation<T>(this RouteHandlerBuilder builder) where T : class =>
        builder.AddEndpointFilter<ValidationFilter<T>>();
}
