using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Blog;
using Shop.Domain.Security;

namespace Shop.Api.Controllers;

// CSRF like every admin write, 409 on a stale row version.
[Route("api/v1/admin/blog")]
[Tags("Admin blog")]
[Authorize]
public sealed class AdminBlogController(IAdminBlogService blog) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.BlogView)]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Http(Results.Ok(await blog.ListAsync(ct)));

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.BlogView)]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        Http(await blog.GetAsync(id, ct), Results.Ok);

    [HttpPost]
    [Authorize(Policy = Permissions.BlogCreate)]
    public async Task<IActionResult> Create([FromBody] CreateBlogPostRequest request, CancellationToken ct) =>
        Http(await blog.CreateAsync(request, ct), post => Results.Created($"/api/v1/admin/blog/{post.Id}", post));

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.BlogUpdate)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateBlogPostRequest request, CancellationToken ct) =>
        Http(await blog.UpdateAsync(id, request, ct), Results.Ok);

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.BlogDelete)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        Http(await blog.DeleteAsync(id, ct), Results.NoContent);

    [HttpGet("{id:int}/audit")]
    [Authorize(Policy = Permissions.BlogView)]
    public async Task<IActionResult> Audit(int id, CancellationToken ct) =>
        Http(await blog.AuditAsync(id, ct), Results.Ok);
}
