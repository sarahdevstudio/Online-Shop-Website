using System.Security.Claims;
using Shop.Application.Common;
using Shop.Domain.Security;

namespace Shop.Api.Infrastructure;

internal sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public bool IsInRole(string role) => Principal?.IsInRole(role) == true;

    public bool HasPermission(string permission) =>
        Principal?.HasClaim(Permissions.ClaimType, permission) == true;
}
