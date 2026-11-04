using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Common;
using Shop.Application.Routing;
using Shop.Domain.Routing;
using Shop.Domain.Security;

namespace Shop.Api.Controllers;

/// <summary>
/// Admin side of the route table (docs/11 C.3, decision C-4). Reading and changing an entity's own path needs that entity's
/// manage permission (category/product: Catalog.Manage, info page: Content.Manage, blog post: Blog.Manage); listing, searching,
/// releasing and auditing redirects needs Routes.Manage. CSRF like every admin write, 409 on a stale row version.
/// </summary>
[Route("api/v1/admin/routes")]
[Tags("Admin routes")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminRoutesController(IRouteService routes, ICurrentUser currentUser) : ApiControllerBase
{
    private static string? PermissionFor(string type) => type switch
    {
        RouteTargetTypes.Category or RouteTargetTypes.Product => Permissions.CatalogManage,
        RouteTargetTypes.ContentPage => Permissions.ContentManage,
        RouteTargetTypes.BlogPost => Permissions.BlogManage,
        _ => null,
    };

    private IActionResult? Denied(string type) =>
        PermissionFor(type) is not { } permission
            ? Http(Error.NotFound("target_not_found", "api.error.route-target-not-found"))
            : currentUser.HasPermission(permission) ? null : Http(Error.Forbidden("forbidden", "api.error.forbidden"));

    [HttpGet("{type}/{id:int}")]
    public async Task<IActionResult> Get(string type, int id, CancellationToken ct) =>
        Denied(type) ?? Http(await routes.GetAsync(type, id, ct), Results.Ok);

    [HttpPut("{type}/{id:int}")]
    public async Task<IActionResult> Change(string type, int id, [FromBody] ChangeRoutePathRequest request, CancellationToken ct) =>
        Denied(type) ?? Http(await routes.ChangeAsync(type, id, request, ct), Results.Ok);

    [HttpGet("redirects")]
    [Authorize(Policy = Permissions.RoutesManage)]
    public async Task<IActionResult> Redirects([FromQuery] string? q, [FromQuery] int page = 1, CancellationToken ct = default) =>
        Http(Results.Ok(await routes.ListRedirectsAsync(q, page, ct)));

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.RoutesManage)]
    public async Task<IActionResult> Release(int id, CancellationToken ct) =>
        Http(await routes.ReleaseAsync(id, ct), Results.NoContent);

    [HttpGet("audit")]
    [Authorize(Policy = Permissions.RoutesManage)]
    public async Task<IActionResult> Audit([FromQuery] int page = 1, CancellationToken ct = default) =>
        Http(Results.Ok(await routes.AuditAsync(page, ct)));
}
