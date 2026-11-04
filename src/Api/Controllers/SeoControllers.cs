using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Common;
using Shop.Application.Seo;
using Shop.Domain.Routing;
using Shop.Domain.Security;

namespace Shop.Api.Controllers;

// SEO fields, FAQ and the category guide are edited with the entity they belong to, so the entity's own permission decides:
// Catalog.Manage (products, categories), Content.Manage (info pages), Blog.Manage (posts). CSRF like every admin write, 409 on a stale version.
[Route("api/v1/admin/seo/{type}/{id:int}")]
[Tags("Admin SEO")]
[Authorize]
public sealed class AdminSeoController(IAdminSeoService seo, ICurrentUser currentUser) : ApiControllerBase
{
    private static string? PermissionFor(string type) => type switch
    {
        RouteTargetTypes.Product or RouteTargetTypes.Category => Permissions.CatalogManage,
        RouteTargetTypes.ContentPage => Permissions.ContentManage,
        RouteTargetTypes.BlogPost => Permissions.BlogManage,
        _ => null,
    };

    private IActionResult? Gate(string type) =>
        PermissionFor(type) is not { } permission ? Http(Error.NotFound("seo_type_unknown", "api.error.requested-item-not-found"))
        : currentUser.HasPermission(permission) ? null
        : Http(Error.Forbidden("forbidden", "api.error.forbidden"));

    [HttpGet]
    public async Task<IActionResult> Get(string type, int id, CancellationToken ct) =>
        Gate(type) ?? Http(await seo.GetAsync(type, id, ct), Results.Ok);

    [HttpPut]
    public async Task<IActionResult> Save(string type, int id, [FromBody] SaveSeoRequest request, CancellationToken ct) =>
        Gate(type) ?? Http(await seo.SaveAsync(type, id, request, ct), Results.Ok);
}

[Route("api/v1/catalog/seo")]
[Tags("Catalog")]
[AllowAnonymous]
public sealed class CatalogSeoController(IPublicSeoService seo) : ApiControllerBase
{
    [HttpGet("noindex")]
    public async Task<IActionResult> NoIndex(CancellationToken ct) => Http(Results.Ok(await seo.GetNoIndexAsync(ct)));

    [HttpGet("{type}/{slug}")]
    public async Task<IActionResult> Get(string type, string slug, CancellationToken ct) =>
        RouteTargetTypes.IsKnown(type)
            ? Http(await seo.GetAsync(type, slug, ct), Results.Ok)
            : Http(Error.NotFound("seo_type_unknown", "api.error.requested-item-not-found"));
}
