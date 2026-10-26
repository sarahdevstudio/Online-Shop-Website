using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Shop.Api.Infrastructure;

/// <summary>
/// Last-resort mapping of unhandled exceptions to RFC 7807 responses. Details and stack traces are
/// logged, never returned to the caller.
/// </summary>
internal sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (httpContext.RequestAborted.IsCancellationRequested && exception is OperationCanceledException)
        {
            // Client went away: nothing to report, and not an error.
            httpContext.Response.StatusCode = 499;
            return true;
        }

        var (status, title) = exception switch
        {
            BadHttpRequestException => (StatusCodes.Status400BadRequest, PersianProblemText.MalformedRequest),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, PersianProblemText.ConcurrentUpdate),
            _ => (StatusCodes.Status500InternalServerError, PersianProblemText.Unexpected),
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, LogPaths.Mask(httpContext.Request.Path));
        }
        else
        {
            logger.LogWarning("Request failed with {Status}: {ExceptionType}", status, exception.GetType().Name);
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = null, // never let the framework attach exception details
            ProblemDetails = new ProblemDetails { Status = status, Title = title },
        });
    }
}
