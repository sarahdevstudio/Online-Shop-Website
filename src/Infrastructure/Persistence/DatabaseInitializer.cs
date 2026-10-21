using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shop.Domain.Security;
using Shop.Infrastructure.Identity;

namespace Shop.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Apply pending EF migrations at startup. Enabled for development and tests only.</summary>
    public bool MigrateOnStartup { get; set; }
}

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>Optional first admin. Supply via environment variables or user-secrets, never committed config.</summary>
    public string? AdminEmail { get; set; }
    public string? AdminPassword { get; set; }
    public string AdminFullName { get; set; } = "Administrator";
}

/// <summary>Idempotent startup work: optional migration, role and permission sync, optional first admin.</summary>
public sealed class DatabaseInitializer(
    ShopDbContext db,
    RoleManager<ApplicationRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IOptions<DatabaseOptions> databaseOptions,
    IOptions<SeedOptions> seedOptions,
    TimeProvider clock,
    ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken ct)
    {
        if (databaseOptions.Value.MigrateOnStartup)
        {
            logger.LogInformation("Applying database migrations");
            await db.Database.MigrateAsync(ct);
        }

        await SyncRolesAndPermissionsAsync();
        await EnsureAdminAsync();
    }

    private async Task SyncRolesAndPermissionsAsync()
    {
        foreach (var roleName in Roles.All)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                var created = await roleManager.CreateAsync(new ApplicationRole(roleName));
                EnsureSucceeded(created, $"create role '{roleName}'");
                role = await roleManager.FindByNameAsync(roleName)
                    ?? throw new InvalidOperationException($"Role '{roleName}' was not found after creation.");
            }

            var desired = Roles.PermissionsFor(roleName).ToHashSet(StringComparer.Ordinal);
            var existing = (await roleManager.GetClaimsAsync(role))
                .Where(c => c.Type == Permissions.ClaimType)
                .ToList();

            foreach (var stale in existing.Where(c => !desired.Contains(c.Value)))
            {
                EnsureSucceeded(await roleManager.RemoveClaimAsync(role, stale), $"remove permission '{stale.Value}'");
            }

            var present = existing.Select(c => c.Value).ToHashSet(StringComparer.Ordinal);
            foreach (var permission in desired.Where(p => !present.Contains(p)))
            {
                var claim = new System.Security.Claims.Claim(Permissions.ClaimType, permission);
                EnsureSucceeded(await roleManager.AddClaimAsync(role, claim), $"add permission '{permission}'");
            }
        }
    }

    private async Task EnsureAdminAsync()
    {
        var seed = seedOptions.Value;
        if (string.IsNullOrWhiteSpace(seed.AdminEmail) || string.IsNullOrEmpty(seed.AdminPassword))
        {
            return;
        }

        var email = seed.AdminEmail.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var admin = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        };

        EnsureSucceeded(await userManager.CreateAsync(admin, seed.AdminPassword), "create seed admin");
        EnsureSucceeded(await userManager.AddToRoleAsync(admin, Roles.Admin), "assign Admin role");
        logger.LogInformation("Seed admin account created");
    }

    private static void EnsureSucceeded(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            // Codes only: descriptions can echo input such as passwords or emails.
            var codes = string.Join(", ", result.Errors.Select(e => e.Code));
            throw new InvalidOperationException($"Failed to {action}: {codes}");
        }
    }
}
