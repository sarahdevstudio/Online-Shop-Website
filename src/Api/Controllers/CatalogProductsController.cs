using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Api.Infrastructure;
using Shop.Application.Catalog;

namespace Shop.Api.Controllers;

[Route("api/v1/catalog/products")]
[Tags("Catalog")]
[AllowAnonymous]
public sealed class CatalogProductsController(ICatalogService catalog) : ApiControllerBase
{
    // ?category=&q=&brand=a,b&<attributeCode>=slug1,slug2&minPrice=&maxPrice=&inStock=&sort=&page=&pageSize=
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var query = ProductListQueryParser.Parse(Request.Query.Select(pair => (pair.Key, (IReadOnlyList<string?>)pair.Value.ToArray())));
        if (!query.IsSuccess)
        {
            return Http(query.Error!);
        }

        return Http(await catalog.GetProductsAsync(query.Value, ct), Results.Ok);
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Get(string slug, CancellationToken ct) =>
        Http(await catalog.GetProductAsync(slug, ct), Results.Ok);
}
