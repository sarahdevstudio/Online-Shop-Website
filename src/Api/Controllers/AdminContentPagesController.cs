using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Content;
using Shop.Domain.Security;

namespace Shop.Api.Controllers;

// CSRF like every admin write, 409 on a stale row version.
[Route("api/v1/admin/content-pages")]
[Tags("Admin content")]
[Authorize]
public sealed class AdminContentPagesController(IAdminContentService content) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.ContentView)]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Http(Results.Ok(await content.ListAsync(ct)));

    [HttpGet("{slug}")]
    [Authorize(Policy = Permissions.ContentView)]
    public async Task<IActionResult> Get(string slug, CancellationToken ct) =>
        Http(await content.GetAsync(slug, ct), Results.Ok);

    [HttpPut("{slug}")]
    [Authorize(Policy = Permissions.ContentUpdate)]
    public async Task<IActionResult> Update(string slug, [FromBody] UpdateContentPageRequest request, CancellationToken ct) =>
        Http(await content.UpdateAsync(slug, request, ct), Results.Ok);

    [HttpGet("{slug}/audit")]
    [Authorize(Policy = Permissions.ContentView)]
    public async Task<IActionResult> Audit(string slug, CancellationToken ct) =>
        Http(await content.AuditAsync(slug, ct), Results.Ok);
}
