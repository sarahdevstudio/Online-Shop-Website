using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Blog;

namespace Shop.Api.Controllers;

// Public: published posts only. The storefront renders the Markdown (raw HTML is never rendered there).
[Route("api/v1/catalog/blog")]
[Tags("Blog")]
[AllowAnonymous]
public sealed class BlogController(IBlogService blog) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery][ModelBinder(typeof(DocumentationOnlyModelBinder))] int page = 1, CancellationToken ct = default)
    {
        if (!QueryBinding.TryGet(Request.Query, "page", out var pageValue))
        {
            return BadRequest();
        }

        return Http(Results.Ok(await blog.ListAsync(pageValue ?? 1, ct)));
    }

    [HttpGet("sitemap")]
    public async Task<IActionResult> Sitemap(CancellationToken ct) =>
        Http(Results.Ok(await blog.SitemapAsync(ct)));

    [HttpGet("{slug}")]
    public async Task<IActionResult> Get(string slug, CancellationToken ct) =>
        Http(await blog.GetAsync(slug, ct), Results.Ok);
}
