using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Settings;
using Shop.Domain.Security;

namespace Shop.Api.Controllers;

// Settings.Manage is its own permission (not Catalog.Manage). Writes need the antiforgery token like every admin call.
[Route("api/v1/admin/settings")]
[Tags("Admin settings")]
[Authorize(Policy = Permissions.SettingsManage)]
public sealed class AdminSettingsController(IAdminSettingsService settings) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Http(Results.Ok(await settings.GetAsync(ct)));

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateStoreSettingsRequest request, CancellationToken ct) =>
        Http(await settings.UpdateAsync(request, ct), Results.Ok);

    [HttpGet("audit")]
    public async Task<IActionResult> Audit([FromQuery] int? page, CancellationToken ct) =>
        Http(Results.Ok(await settings.GetAuditAsync(page ?? 1, ct)));
}
