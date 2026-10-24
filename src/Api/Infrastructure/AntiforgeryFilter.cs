using Microsoft.AspNetCore.Antiforgery;

namespace Shop.Api.Infrastructure;

/// <summary>
/// CSRF protection for the cookie-authenticated API: every unsafe request (POST/PUT/PATCH/DELETE) must
/// carry a valid X-XSRF-TOKEN header obtained from GET /api/v1/auth/csrf.
/// </summary>
internal sealed class AntiforgeryFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;

        // Server-to-server storefront endpoints carry no ambient credentials and are guarded by the storefront key
        // filter registered on the same group (see StoreEndpoints); every other unsafe request needs the token.
        if (context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<StorefrontEndpointMetadata>() is not null)
        {
            return await next(context);
        }

        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)
            || HttpMethods.IsOptions(request.Method) || HttpMethods.IsTrace(request.Method))
        {
            return await next(context);
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.Problem(
                "The antiforgery token is missing or invalid.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad request",
                extensions: new Dictionary<string, object?> { ["code"] = "antiforgery_invalid" });
        }

        return await next(context);
    }
}
