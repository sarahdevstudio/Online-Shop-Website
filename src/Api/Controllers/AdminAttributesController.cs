using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Catalog;
using Shop.Domain.Security;

namespace Shop.Api.Controllers;

[Route("api/v1/admin/attributes")]
[Tags("Admin catalog")]
[Authorize(Policy = Permissions.CatalogManage)]
public sealed class AdminAttributesController(ICatalogAdminService catalog) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Http(Results.Ok(await catalog.GetAttributesAsync(ct)));

    [HttpPost("{id:int}/options")]
    [ValidateRequest<AttributeOptionRequest>]
    public async Task<IActionResult> AddOption(int id, [FromBody] AttributeOptionRequest request, CancellationToken ct) =>
        Http(await catalog.AddAttributeOptionAsync(id, request, ct), o => Results.Created($"/api/v1/admin/attributes/{id}", o));
}
