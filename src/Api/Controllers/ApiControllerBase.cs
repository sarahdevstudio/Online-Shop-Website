using Microsoft.AspNetCore.Mvc;
using Shop.Api.Infrastructure;
using Shop.Application.Common;

namespace Shop.Api.Controllers;

/// <summary>
/// Base of every API controller. Routes are always written out in full (<c>[Route("api/v1/...")]</c>, never a
/// <c>[controller]</c> token) so renaming a class can never change a URL.
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected static IActionResult Http(IResult result) => new HttpResultAdapter(result);

    protected static IActionResult Http<T>(Result<T> result, Func<T, IResult> onSuccess) => Http(result.ToHttpResult(onSuccess));

    protected static IActionResult Http(Result result, Func<IResult> onSuccess) => Http(result.ToHttpResult(onSuccess));

    protected static IActionResult Http(Error error) => Http(error.ToProblem());
}
