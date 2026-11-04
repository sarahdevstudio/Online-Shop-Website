using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Domain.Security;

namespace Shop.Api.Controllers;

public sealed record AdminMeDto(string Username, IReadOnlyList<string> Permissions);

// Any signed-in staff account (at least one permission) may ask what it is allowed to do: the admin panel hides what it cannot use.
[Route("api/v1/admin/me")]
[Tags("Admin account")]
[Authorize]
public sealed class AdminMeController : ApiControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        var permissions = User.FindAll(Permissions.ClaimType).Select(c => c.Value).Where(Permissions.All.Contains).Distinct().Order(StringComparer.Ordinal).ToList();
        return permissions.Count == 0
            ? Http(Results.Forbid())
            : Http(Results.Ok(new AdminMeDto(User.Identity?.Name ?? string.Empty, permissions)));
    }
}
