using Microsoft.AspNetCore.Mvc;

namespace Shop.Api.Controllers;

/// <summary>
/// Lets a controller action return the same <see cref="IResult"/> the minimal API returned, so JSON options, problem
/// bodies (<c>ToProblem</c>), <c>Created</c> locations and status codes are produced by exactly the same code as before.
/// </summary>
public sealed class HttpResultAdapter(IResult result) : IActionResult
{
    public Task ExecuteResultAsync(ActionContext context) => result.ExecuteAsync(context.HttpContext);
}
