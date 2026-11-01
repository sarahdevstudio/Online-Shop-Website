using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Catalog;

namespace Shop.Api.Controllers;

[Route("api/v1/catalog/sitemap")]
[Tags("Catalog")]
[AllowAnonymous]
public sealed class SitemapController(ICatalogService catalog) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Http(Results.Ok(await catalog.GetSitemapAsync(ct)));
}
