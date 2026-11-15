using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Admin;
using Shop.Domain.Security;

namespace Shop.Api.Controllers;

// F2 (docs/19, reports and dashboard) replaces Dashboard with a field-level, permission-aware endpoint (each card
// shown only with its own permission); until then it is gated by Orders.View, the permission its order counts need.
[Route("api/v1/admin")]
[Tags("Admin")]
[Authorize]
public sealed class AdminDashboardController(IDashboardService dashboard) : ApiControllerBase
{
    // Minimal permission-protected endpoint; real admin features arrive with their phases.
    [HttpGet("ping")]
    [Authorize(Policy = Permissions.SettingsView)]
    public IActionResult Ping() => Http(Results.Ok(new { status = "ok" }));

    // Counts only (new / waiting for confirmation / to ship / today / low stock).
    [HttpGet("dashboard")]
    [Authorize(Policy = Permissions.OrdersView)]
    public async Task<IActionResult> Dashboard(CancellationToken ct) => Http(Results.Ok(await dashboard.GetAsync(ct)));
}
