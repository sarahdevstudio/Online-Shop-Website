using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Catalog;
using Shop.Domain.Security;

namespace Shop.Api.Controllers;

[Route("api/v1/admin/brands")]
[Tags("Admin catalog")]
[Authorize(Policy = Permissions.CatalogManage)]
public sealed class AdminBrandsController(ICatalogAdminService catalog) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Http(Results.Ok(await catalog.GetBrandsAsync(ct)));

    [HttpPost]
    [ValidateRequest<BrandRequest>]
    public async Task<IActionResult> Create([FromBody] BrandRequest request, CancellationToken ct) =>
        Http(await catalog.CreateBrandAsync(request, ct), b => Results.Created($"/api/v1/admin/brands/{b.Id}", b));

    [HttpPut("{id:int}")]
    [ValidateRequest<BrandRequest>]
    public async Task<IActionResult> Update(int id, [FromBody] BrandRequest request, CancellationToken ct) =>
        Http(await catalog.UpdateBrandAsync(id, request, ct), Results.Ok);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        Http(await catalog.DeleteBrandAsync(id, ct), Results.NoContent);
}
