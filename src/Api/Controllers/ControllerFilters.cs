using FluentValidation;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;
using Shop.Api.Infrastructure;
using Shop.Application.Orders;

namespace Shop.Api.Controllers;

// Filter order (lower runs first), mirroring how the endpoint filters nested before:
//   -2000  ModelStateInvalidFilter (framework): unbindable request  -> bare 400
//   -1900  RequiredBodyFilter: a body that bound to null            -> bare 400 (unless the action validates it)
//    -900  CsrfFilter  (was the filter on the /api/v1 group)
//    -800  StorefrontApiAttribute (was the filter on the /store group)
//    -700  ValidateRequestAttribute<T> (was WithValidation<T>())
internal static class FilterOrder
{
    public const int RequiredBody = -1900;
    public const int Csrf = -900;
    public const int StorefrontKey = -800;
    public const int Validation = -700;
}

/// <summary>Implemented by the validation attribute so the generic null-body check leaves validated bodies to it.</summary>
public interface IValidatesRequestBody
{
}

/// <summary>
/// CSRF protection for the cookie-authenticated API: every unsafe request must carry a valid X-XSRF-TOKEN header
/// (see <c>GET /api/v1/auth/csrf</c>). Actions on a <see cref="StorefrontApiAttribute"/> controller are exempt.
/// </summary>
internal sealed class CsrfFilter(IAntiforgery antiforgery) : IAsyncActionFilter, IOrderedFilter
{
    public int Order => FilterOrder.Csrf;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        var request = http.Request;
        if (context.ActionDescriptor.EndpointMetadata.OfType<StorefrontApiAttribute>().Any()
            || HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)
            || HttpMethods.IsOptions(request.Method) || HttpMethods.IsTrace(request.Method))
        {
            await next();
            return;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(http);
        }
        catch (AntiforgeryValidationException)
        {
            context.Result = new HttpResultAdapter(Results.Problem(
                PersianProblemText.AntiforgeryInvalid,
                statusCode: StatusCodes.Status400BadRequest,
                title: PersianProblemText.BadRequest,
                extensions: new Dictionary<string, object?> { ["code"] = "antiforgery_invalid" }));
            return;
        }

        await next();
    }
}

/// <summary>
/// A body parameter that bound to null (empty body, or the JSON literal <c>null</c>) is a bare 400, as it was for
/// endpoints without a validator. Actions with <see cref="ValidateRequestAttribute{T}"/> answer it themselves.
/// </summary>
internal sealed class RequiredBodyFilter : IActionFilter, IOrderedFilter
{
    public int Order => FilterOrder.RequiredBody;

    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ActionDescriptor.FilterDescriptors.Any(f => f.Filter is IValidatesRequestBody))
        {
            return;
        }

        foreach (var parameter in context.ActionDescriptor.Parameters.Where(p => p.BindingInfo?.BindingSource == BindingSource.Body))
        {
            if (!context.ActionArguments.TryGetValue(parameter.Name, out var value) || value is null)
            {
                context.Result = new StatusCodeResult(StatusCodes.Status400BadRequest);
                return;
            }
        }
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
    }
}

/// <summary>
/// Server-to-server endpoints of the storefront: no ambient credentials (so no CSRF), but only the Next.js server,
/// which knows the shared key, may call them.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class StorefrontApiAttribute : Attribute, IAsyncActionFilter, IOrderedFilter
{
    public int Order => FilterOrder.StorefrontKey;

    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var options = context.HttpContext.RequestServices.GetRequiredService<IOptions<StorefrontOptions>>().Value;
        var supplied = context.HttpContext.Request.Headers[StorefrontKeyRules.HeaderName].ToString();
        if (!StorefrontKeyRules.Matches(options.ApiKey, supplied))
        {
            context.Result = new HttpResultAdapter(Results.Problem(
                PersianProblemText.StorefrontKeyRequired,
                statusCode: StatusCodes.Status401Unauthorized,
                title: PersianProblemText.Unauthorized,
                extensions: new Dictionary<string, object?> { ["code"] = "storefront_key_invalid" }));
            return Task.CompletedTask;
        }

        return next();
    }
}

/// <summary>Validates the bound request body of type <typeparamref name="T"/> with FluentValidation before the action runs.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ValidateRequestAttribute<T> : Attribute, IAsyncActionFilter, IOrderedFilter, IValidatesRequestBody where T : class
{
    public int Order => FilterOrder.Validation;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.ActionArguments.Values.OfType<T>().FirstOrDefault();
        if (request is null)
        {
            context.Result = new HttpResultAdapter(Results.Problem(
                PersianProblemText.RequestBodyRequired,
                statusCode: StatusCodes.Status400BadRequest,
                title: PersianProblemText.BadRequest));
            return;
        }

        var validator = context.HttpContext.RequestServices.GetRequiredService<IValidator<T>>();
        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
        if (!result.IsValid)
        {
            context.Result = new HttpResultAdapter(Results.ValidationProblem(result.ToDictionary(), title: PersianProblemText.Validation));
            return;
        }

        await next();
    }
}
