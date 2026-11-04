using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Catalog;
using Shop.Application.Search;

namespace Shop.Api.Controllers;

/// <summary>Suggestions for the header search box: a few products and categories, typo-tolerant, from an in-memory index.</summary>
[Route("api/v1/catalog/suggest")]
[Tags("Catalog")]
[AllowAnonymous]
public sealed class CatalogSuggestController(ICatalogSearch search) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Suggest([FromQuery] string? q, CancellationToken ct) =>
        Http(Results.Ok(await search.SuggestAsync(q is { Length: > ProductListQuery.QMaxLength } ? q[..ProductListQuery.QMaxLength] : q, ct)));
}
