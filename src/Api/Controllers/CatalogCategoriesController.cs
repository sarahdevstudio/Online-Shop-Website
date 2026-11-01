using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Catalog;

namespace Shop.Api.Controllers;

[Route("api/v1/catalog/categories")]
[Tags("Catalog")]
[AllowAnonymous]
public sealed class CatalogCategoriesController(ICatalogService catalog) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Tree(CancellationToken ct) =>
        Http(Results.Ok(await catalog.GetCategoryTreeAsync(ct)));

    [HttpGet("{slug}")]
    public async Task<IActionResult> Get(string slug, CancellationToken ct) =>
        Http(await catalog.GetCategoryAsync(slug, ct), Results.Ok);
}
