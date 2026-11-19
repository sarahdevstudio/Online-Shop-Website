using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Shop.Domain.Security;

namespace Shop.Infrastructure.Identity;

/// <summary>
/// The sign-in ticket (<c>shop_auth</c> cookie) keeps identity only (id, username, role names, security stamp) —
/// never permission claims, so its size no longer grows with the permission count, and a role or permission change
/// never needs the affected accounts to sign in again (see <see cref="IPermissionCache"/> and
/// <see cref="LivePermissionsClaimsTransformation"/>, which resolve the live set on every request instead).
/// </summary>
internal sealed class MinimalClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager, RoleManager<ApplicationRole> roleManager, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser, ApplicationRole>(userManager, roleManager, options)
{
    public override async Task<ClaimsPrincipal> CreateAsync(ApplicationUser user)
    {
        var principal = await base.CreateAsync(user);
        var identity = (ClaimsIdentity)principal.Identity!;
        foreach (var claim in identity.FindAll(Permissions.ClaimType).ToList())
        {
            identity.RemoveClaim(claim);
        }

        return principal;
    }
}
