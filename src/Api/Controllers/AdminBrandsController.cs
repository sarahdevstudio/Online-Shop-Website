using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Catalog;
using Shop.Domain.Security;

namespace Shop.Api.Controllers;

[Route("api/v1/admin/brands")]
[Tags("Admin catalog")]
[Authorize]
public sealed class AdminBrandsController(ICatalogAdminService catalog) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.CatalogView)]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Http(Results.Ok(await catalog.GetBrandsAsync(ct)));

    [HttpPost]
    [Authorize(Policy = Permissions.CatalogCreate)]
    [ValidateRequest<BrandRequest>]
    public async Task<IActionResult> Create([FromBody] BrandRequest request, CancellationToken ct) =>
        Http(await catalog.CreateBrandAsync(request, ct), b => Results.Created($"/api/v1/admin/brands/{b.Id}", b));

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.CatalogUpdate)]
    [ValidateRequest<BrandRequest>]
    public async Task<IActionResult> Update(int id, [FromBody] BrandRequest request, CancellationToken ct) =>
        Http(await catalog.UpdateBrandAsync(id, request, ct), Results.Ok);

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.CatalogDelete)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        Http(await catalog.DeleteBrandAsync(id, ct), Results.NoContent);
}
