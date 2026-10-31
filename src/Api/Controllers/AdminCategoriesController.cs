using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Catalog;
using Shop.Domain.Security;

namespace Shop.Api.Controllers;

[Route("api/v1/admin/categories")]
[Tags("Admin catalog")]
[Authorize(Policy = Permissions.CatalogManage)]
public sealed class AdminCategoriesController(ICatalogAdminService catalog) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Http(Results.Ok(await catalog.GetCategoriesAsync(ct)));

    [HttpPost]
    [ValidateRequest<CategoryRequest>]
    public async Task<IActionResult> Create([FromBody] CategoryRequest request, CancellationToken ct) =>
        Http(await catalog.CreateCategoryAsync(request, ct), c => Results.Created($"/api/v1/admin/categories/{c.Id}", c));

    [HttpPut("{id:int}")]
    [ValidateRequest<CategoryRequest>]
    public async Task<IActionResult> Update(int id, [FromBody] CategoryRequest request, CancellationToken ct) =>
        Http(await catalog.UpdateCategoryAsync(id, request, ct), Results.Ok);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        Http(await catalog.DeleteCategoryAsync(id, ct), Results.NoContent);
}
