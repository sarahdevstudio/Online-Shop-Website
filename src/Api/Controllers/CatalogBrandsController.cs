using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Catalog;

namespace Shop.Api.Controllers;

[Route("api/v1/catalog/brands")]
[Tags("Catalog")]
[AllowAnonymous]
public sealed class CatalogBrandsController(ICatalogService catalog) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Http(Results.Ok(await catalog.GetBrandsAsync(ct)));
}
