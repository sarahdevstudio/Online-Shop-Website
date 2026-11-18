using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Admin;
using Shop.Domain.Security;

namespace Shop.Api.Controllers;

// Dashboard (docs/19 F2) is field-level permission-aware: the service includes each card only when the caller
// holds its own permission, so the one endpoint just needs a signed-in admin.
[Route("api/v1/admin")]
[Tags("Admin")]
[Authorize]
public sealed class AdminDashboardController(IDashboardService dashboard) : ApiControllerBase
{
    // Minimal permission-protected endpoint; real admin features arrive with their phases.
    [HttpGet("ping")]
    [Authorize(Policy = Permissions.SettingsView)]
    public IActionResult Ping() => Http(Results.Ok(new { status = "ok" }));

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        // Same rule as /admin/me: a signed-in account with no permission at all is a customer, not staff.
        if (!User.FindAll(Permissions.ClaimType).Any(c => Permissions.All.Contains(c.Value)))
        {
            return Http(Results.Forbid());
        }

        return Http(Results.Ok(await dashboard.GetAsync(ct)));
    }
}
