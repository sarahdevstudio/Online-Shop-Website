using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Content;

namespace Shop.Api.Controllers;

// Public: the storefront renders the Markdown (raw HTML is never rendered there).
[Route("api/v1/catalog/pages")]
[Tags("Content")]
[AllowAnonymous]
public sealed class ContentPagesController(IContentService content) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Http(Results.Ok(await content.ListAsync(ct)));

    [HttpGet("{slug}")]
    public async Task<IActionResult> Get(string slug, CancellationToken ct) =>
        Http(await content.GetAsync(slug, ct), Results.Ok);
}
