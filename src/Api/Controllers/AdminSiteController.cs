using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Api.Infrastructure;
using Shop.Application.Site;
using Shop.Domain.Security;

namespace Shop.Api.Controllers;

// Site.Manage (its own permission), CSRF like every admin write, 409 on a stale draft version. {kind} is
// theme | home | chrome | seo | badges.
[Route("api/v1/admin/site")]
[Tags("Admin site")]
[Authorize(Policy = Permissions.SiteManage)]
public sealed class AdminSiteController(IAdminSiteService site, ISiteConfigService published) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Overview(CancellationToken ct) =>
        Http(Results.Ok(await site.OverviewAsync(ct)));

    [HttpGet("audit")]
    public async Task<IActionResult> Audit([FromQuery] string? kind, [FromQuery] int? page, CancellationToken ct) =>
        Http(await site.AuditAsync(kind, page ?? 1, ct), Results.Ok);

    /// <summary>The compiled document for the storefront's preview: <c>state=draft</c> (drafts applied) or <c>published</c>.</summary>
    [HttpGet("compiled")]
    public async Task<IActionResult> Compiled([FromQuery] string? state, CancellationToken ct)
    {
        if (state is not ("draft" or "published"))
        {
            return Http(Results.ValidationProblem(
                new Dictionary<string, string[]> { ["state"] = ["مقدار باید draft یا published باشد."] },
                title: PersianProblemText.BadRequest));
        }

        return Http(Results.Ok(state == "draft" ? await site.CompiledDraftAsync(ct) : await published.GetPublishedAsync(ct)));
    }

    // Multipart upload. No framework antiforgery middleware is registered, so the API-wide X-XSRF-TOKEN filter
    // (CsrfFilter) is the only CSRF check.
    [HttpPost("images")]
    [RequireMultipartForm]
    public async Task<IActionResult> UploadImage([Required] IFormFile file, [FromForm] string? alt, [FromForm] string? purpose, CancellationToken ct)
    {
        if (file is null)
        {
            return Http(Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: PersianProblemText.MalformedRequest));
        }

        await using var stream = file.OpenReadStream();
        var upload = new SiteImageUpload(stream, file.FileName, file.ContentType, file.Length, alt, purpose ?? string.Empty);
        return Http(await site.UploadImageAsync(upload, ct), image => Results.Created($"/api/v1/admin/site/images/{image.Id}", image));
    }

    [HttpGet("{kind}")]
    public async Task<IActionResult> Get(string kind, CancellationToken ct) =>
        Http(await site.GetAsync(kind, ct), Results.Ok);

    [HttpPut("{kind}/draft")]
    public async Task<IActionResult> SaveDraft(string kind, [FromBody] SaveSiteDraftRequest request, CancellationToken ct) =>
        Http(await site.SaveDraftAsync(kind, request, ct), Results.Ok);

    [HttpPost("{kind}/publish")]
    public async Task<IActionResult> Publish(string kind, [FromBody] PublishSiteRequest request, CancellationToken ct) =>
        Http(await site.PublishAsync(kind, request, ct), Results.Ok);

    [HttpPost("{kind}/discard")]
    public async Task<IActionResult> Discard(string kind, [FromBody] DiscardSiteDraftRequest request, CancellationToken ct) =>
        Http(await site.DiscardAsync(kind, request, ct), Results.Ok);

    [HttpPost("{kind}/restore/{version:int}")]
    public async Task<IActionResult> Restore(string kind, int version, [FromBody] RestoreSiteVersionRequest request, CancellationToken ct) =>
        Http(await site.RestoreAsync(kind, version, request, ct), Results.Ok);

    [HttpGet("{kind}/versions/{version:int}")]
    public async Task<IActionResult> GetVersion(string kind, int version, CancellationToken ct) =>
        Http(await site.GetVersionAsync(kind, version, ct), Results.Ok);
}
