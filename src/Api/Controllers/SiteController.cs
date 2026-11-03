using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Site;

namespace Shop.Api.Controllers;

// The published site configuration (theme, home blocks, header/footer, SEO defaults, trust badges) in one small document.
// The storefront caches it for 60 s and falls back to built-in defaults when this call fails.
[Route("api/v1/catalog/site")]
[Tags("Catalog")]
[AllowAnonymous]
public sealed class SiteController(ISiteConfigService site) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        Response.Headers.CacheControl = "public, max-age=30";
        return Http(Results.Ok(await site.GetPublishedAsync(ct)));
    }
}
