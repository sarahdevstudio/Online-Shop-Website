using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Routing;

namespace Shop.Api.Controllers;

/// <summary>
/// What does this public path point to? Answered from memory (no SQL), always 200 (docs/11 C.3): an entity, a one-hop redirect to
/// its current path, <c>gone</c> (410) or <c>notFound</c> (404). The query string of the page never reaches this endpoint.
/// </summary>
[Route("api/v1/catalog/resolve")]
[Tags("Catalog")]
[AllowAnonymous]
public sealed class CatalogResolveController(IRouteTable routes) : ApiControllerBase
{
    [HttpGet]
    public IActionResult Resolve([FromQuery] string? path) =>
        Http(Results.Ok(routes.Resolve(path is { Length: <= 400 } ? path : null)));
}
