using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Translations;
using Shop.Domain.Security;

namespace Shop.Api.Controllers;

// Translations.Manage (its own permission), CSRF like every admin write, 409 on a stale row version. The bundle of the admin
// panel's own texts is readable by any signed-in admin: the panel needs its labels whatever it may edit.
[Route("api/v1/admin/translations")]
[Tags("Admin translations")]
public sealed class AdminTranslationsController(IAdminTranslationService translations) : ApiControllerBase
{
    [HttpGet("bundle")]
    [Authorize(Roles = Roles.Admin)]
    public IActionResult Bundle([FromQuery] string? prefix)
    {
        Response.Headers.CacheControl = "private, max-age=30";
        return Http(Results.Ok(Translate.Current.Bundle(["admin.", "common."], prefix)));
    }

    [HttpGet]
    [Authorize(Policy = Permissions.TranslationsManage)]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] string? area, [FromQuery] bool? edited, [FromQuery] int? page, CancellationToken ct) =>
        Http(Results.Ok(await translations.ListAsync(q, area, edited == true, page ?? 1, ct)));

    [HttpGet("areas")]
    [Authorize(Policy = Permissions.TranslationsManage)]
    public async Task<IActionResult> Areas(CancellationToken ct) =>
        Http(Results.Ok(await translations.AreasAsync(ct)));

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.TranslationsManage)]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        Http(await translations.GetAsync(id, ct), Results.Ok);

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.TranslationsManage)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTranslationRequest request, CancellationToken ct) =>
        Http(await translations.UpdateAsync(id, request, ct), Results.Ok);

    [HttpPost("{id:int}/reset")]
    [Authorize(Policy = Permissions.TranslationsManage)]
    public async Task<IActionResult> Reset(int id, [FromBody] ResetTranslationRequest request, CancellationToken ct) =>
        Http(await translations.ResetAsync(id, request, ct), Results.Ok);

    [HttpGet("{id:int}/audit")]
    [Authorize(Policy = Permissions.TranslationsManage)]
    public async Task<IActionResult> Audit(int id, CancellationToken ct) =>
        Http(await translations.AuditAsync(id, ct), Results.Ok);
}
