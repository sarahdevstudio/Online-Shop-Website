using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Admin;
using Shop.Domain.Security;

namespace Shop.Api.Controllers;

[Route("api/v1/admin")]
[Tags("Admin")]
public sealed class AdminDashboardController(IDashboardService dashboard) : ApiControllerBase
{
    // Minimal permission-protected endpoint; real admin features arrive with their phases.
    [HttpGet("ping")]
    [Authorize(Policy = Permissions.SettingsManage)]
    public IActionResult Ping() => Http(Results.Ok(new { status = "ok" }));

    // Counts only (new / waiting for confirmation / to ship / today / low stock).
    [HttpGet("dashboard")]
    [Authorize(Policy = Permissions.OrdersManage)]
    public async Task<IActionResult> Dashboard(CancellationToken ct) => Http(Results.Ok(await dashboard.GetAsync(ct)));
}
