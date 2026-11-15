using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Catalog;
using Shop.Domain.Security;

namespace Shop.Api.Controllers;

[Route("api/v1/admin/categories")]
[Tags("Admin catalog")]
[Authorize]
public sealed class AdminCategoriesController(ICatalogAdminService catalog) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.CatalogView)]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Http(Results.Ok(await catalog.GetCategoriesAsync(ct)));

    [HttpPost]
    [Authorize(Policy = Permissions.CatalogCreate)]
    [ValidateRequest<CategoryRequest>]
    public async Task<IActionResult> Create([FromBody] CategoryRequest request, CancellationToken ct) =>
        Http(await catalog.CreateCategoryAsync(request, ct), c => Results.Created($"/api/v1/admin/categories/{c.Id}", c));

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.CatalogUpdate)]
    [ValidateRequest<CategoryRequest>]
    public async Task<IActionResult> Update(int id, [FromBody] CategoryRequest request, CancellationToken ct) =>
        Http(await catalog.UpdateCategoryAsync(id, request, ct), Results.Ok);

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.CatalogDelete)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        Http(await catalog.DeleteCategoryAsync(id, ct), Results.NoContent);
}
