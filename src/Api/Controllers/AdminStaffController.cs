using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Staff;
using Shop.Domain.Security;

namespace Shop.Api.Controllers;

// User.Manage is its own permission. CSRF like every admin write, 409 on a stale stamp, audit in StaffAuditLogs. Changes to roles
// and permissions, creating an account and resetting a password need the caller's own password again (step-up).
[Route("api/v1/admin/staff")]
[Tags("Admin staff")]
[Authorize(Policy = Permissions.UserManage)]
public sealed class AdminStaffController(IStaffService staff) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Overview(CancellationToken ct) => Http(Results.Ok(await staff.GetOverviewAsync(ct)));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStaffRequest request, CancellationToken ct) =>
        Http(await staff.CreateAsync(request, ct), user => Results.Created($"/api/v1/admin/staff/{user.Id}", user));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStaffRequest request, CancellationToken ct) =>
        Http(await staff.UpdateAsync(id, request, ct), Results.Ok);

    [HttpPut("{id:guid}/roles")]
    public async Task<IActionResult> SetRoles(Guid id, [FromBody] SetStaffRolesRequest request, CancellationToken ct) =>
        Http(await staff.SetRolesAsync(id, request, ct), Results.Ok);

    [HttpPut("{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] SetStaffActiveRequest request, CancellationToken ct) =>
        Http(await staff.SetActiveAsync(id, request, ct), Results.Ok);

    [HttpPut("{id:guid}/password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetStaffPasswordRequest request, CancellationToken ct) =>
        Http(await staff.ResetPasswordAsync(id, request, ct), Results.Ok);

    [HttpGet("audit")]
    public async Task<IActionResult> Audit([FromQuery] int? page, CancellationToken ct) =>
        Http(Results.Ok(await staff.GetAuditAsync(page ?? 1, ct)));

    [HttpPost("roles")]
    public async Task<IActionResult> CreateRole([FromBody] SaveRoleRequest request, CancellationToken ct) =>
        Http(await staff.SaveRoleAsync(null, request, ct), role => Results.Created($"/api/v1/admin/staff/roles/{role.Id}", role));

    [HttpPut("roles/{id:guid}")]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] SaveRoleRequest request, CancellationToken ct) =>
        Http(await staff.SaveRoleAsync(id, request, ct), Results.Ok);

    [HttpPost("roles/{id:guid}/delete")]
    public async Task<IActionResult> DeleteRole(Guid id, [FromBody] DeleteRoleRequest request, CancellationToken ct) =>
        Http(await staff.DeleteRoleAsync(id, request, ct), Results.NoContent);
}
