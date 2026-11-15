using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Common;
using Shop.Application.Routing;
using Shop.Domain.Routing;
using Shop.Domain.Security;

namespace Shop.Api.Controllers;

/// <summary>
/// Admin side of the route table (docs/11 C.3, decision C-4). Reading and changing an entity's own path needs that
/// entity's own view/update permission (category/product: Catalog.*, info page: Content.*, blog post: Blog.*);
/// listing, searching, releasing and auditing redirects needs Routes.*. CSRF like every admin write, 409 on a
/// stale row version.
/// </summary>
[Route("api/v1/admin/routes")]
[Tags("Admin routes")]
[Authorize]
public sealed class AdminRoutesController(IRouteService routes, ICurrentUser currentUser) : ApiControllerBase
{
    private static string? ViewPermissionFor(string type) => type switch
    {
        RouteTargetTypes.Category or RouteTargetTypes.Product => Permissions.CatalogView,
        RouteTargetTypes.ContentPage => Permissions.ContentView,
        RouteTargetTypes.BlogPost => Permissions.BlogView,
        _ => null,
    };

    private static string? UpdatePermissionFor(string type) => type switch
    {
        RouteTargetTypes.Category or RouteTargetTypes.Product => Permissions.CatalogUpdate,
        RouteTargetTypes.ContentPage => Permissions.ContentUpdate,
        RouteTargetTypes.BlogPost => Permissions.BlogUpdate,
        _ => null,
    };

    private IActionResult? Denied(string? permission) =>
        permission is null
            ? Http(Error.NotFound("target_not_found", "api.error.route-target-not-found"))
            : currentUser.HasPermission(permission) ? null : Http(Error.Forbidden("forbidden", "api.error.forbidden"));

    [HttpGet("{type}/{id:int}")]
    public async Task<IActionResult> Get(string type, int id, CancellationToken ct) =>
        Denied(ViewPermissionFor(type)) ?? Http(await routes.GetAsync(type, id, ct), Results.Ok);

    [HttpPut("{type}/{id:int}")]
    public async Task<IActionResult> Change(string type, int id, [FromBody] ChangeRoutePathRequest request, CancellationToken ct) =>
        Denied(UpdatePermissionFor(type)) ?? Http(await routes.ChangeAsync(type, id, request, ct), Results.Ok);

    [HttpGet("redirects")]
    [Authorize(Policy = Permissions.RoutesView)]
    public async Task<IActionResult> Redirects([FromQuery] string? q, [FromQuery] int page = 1, CancellationToken ct = default) =>
        Http(Results.Ok(await routes.ListRedirectsAsync(q, page, ct)));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.RoutesRelease)]
    public async Task<IActionResult> Release(int id, CancellationToken ct) =>
        Http(await routes.ReleaseAsync(id, ct), Results.NoContent);

    [HttpGet("audit")]
    [Authorize(Policy = Permissions.RoutesView)]
    public async Task<IActionResult> Audit([FromQuery] int page = 1, CancellationToken ct = default) =>
        Http(Results.Ok(await routes.AuditAsync(page, ct)));
}
