using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Catalog;
using Shop.Domain.Security;

namespace Shop.Api.Controllers;

[Route("api/v1/admin/images")]
[Tags("Admin catalog")]
[Authorize(Policy = Permissions.CatalogManage)]
public sealed class AdminImagesController(ICatalogAdminService catalog) : ApiControllerBase
{
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        Http(await catalog.DeleteImageAsync(id, ct), Results.NoContent);
}
