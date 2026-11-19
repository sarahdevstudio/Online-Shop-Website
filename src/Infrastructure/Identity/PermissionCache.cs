using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Shop.Domain.Security;

namespace Shop.Infrastructure.Identity;

/// <summary>
/// The current effective permissions of a user — their own direct claims plus every role's claims — resolved live
/// and cached briefly (not carried in the sign-in cookie — see <see cref="MinimalClaimsPrincipalFactory"/>). A
/// deactivated user resolves to no permissions (defense in depth; their session already ends outright via the
/// security-stamp check).
/// </summary>
public interface IPermissionCache
{
    Task<IReadOnlyList<string>> GetAsync(Guid userId, CancellationToken ct);

    /// <summary>After one user's own roles change.</summary>
    void Invalidate(Guid userId);

    /// <summary>After a role's own permissions change — cheap: bumps one counter instead of enumerating members.</summary>
    void InvalidateAll();
}

internal sealed class PermissionCache(IMemoryCache cache, UserManager<ApplicationUser> users, RoleManager<ApplicationRole> roles) : IPermissionCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    // Kept in the (singleton) memory cache itself, not a field, so every scoped instance of this class agrees on it.
    private const string GenerationKey = "permission-cache-generation";

    public async Task<IReadOnlyList<string>> GetAsync(Guid userId, CancellationToken ct)
    {
        var key = CacheKey(userId);
        if (cache.TryGetValue(key, out IReadOnlyList<string>? cached))
        {
            return cached!;
        }

        var resolved = await ResolveAsync(userId);
        cache.Set(key, resolved, Ttl);
        return resolved;
    }

    public void Invalidate(Guid userId) => cache.Remove(CacheKey(userId));

    public void InvalidateAll() => cache.Set(GenerationKey, Generation() + 1);

    private string CacheKey(Guid userId) => $"permissions:{Generation()}:{userId}";

    private int Generation() => cache.GetOrCreate(GenerationKey, _ => 0);

    private async Task<IReadOnlyList<string>> ResolveAsync(Guid userId)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            return [];
        }

        var permissions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var claim in await users.GetClaimsAsync(user))
        {
            if (claim.Type == Permissions.ClaimType)
            {
                permissions.Add(claim.Value);
            }
        }

        foreach (var roleName in await users.GetRolesAsync(user))
        {
            if (await roles.FindByNameAsync(roleName) is not { } role)
            {
                continue;
            }

            foreach (var claim in await roles.GetClaimsAsync(role))
            {
                if (claim.Type == Permissions.ClaimType)
                {
                    permissions.Add(claim.Value);
                }
            }
        }

        return permissions.ToList();
    }
}
