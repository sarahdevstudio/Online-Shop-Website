using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Shop.Domain.Security;

namespace Shop.Infrastructure.Identity;

/// <summary>
/// Adds the signed-in user's current effective permissions to the request's principal (the ticket itself carries
/// none, see <see cref="MinimalClaimsPrincipalFactory"/>), through <see cref="IPermissionCache"/>'s short TTL.
/// </summary>
internal sealed class LivePermissionsClaimsTransformation(IPermissionCache cache) : IClaimsTransformation
{
    private const string LoadedMarker = "permissions-loaded";

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity { IsAuthenticated: true } identity
            || identity.HasClaim(c => c.Type == LoadedMarker)
            || !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return principal;
        }

        foreach (var permission in await cache.GetAsync(userId, CancellationToken.None))
        {
            identity.AddClaim(new Claim(Permissions.ClaimType, permission));
        }

        identity.AddClaim(new Claim(LoadedMarker, "1"));
        return principal;
    }
}
