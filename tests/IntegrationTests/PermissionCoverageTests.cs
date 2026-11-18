using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shop.Domain.Security;
using Shop.Infrastructure.Identity;
using Shop.Infrastructure.Persistence;

namespace Shop.IntegrationTests;

/// <summary>
/// F1 (docs/08): every admin action must require a real, known permission — never left open with only
/// "authenticated", and never a typo that silently falls through to no enforcement (an unknown policy name makes
/// ASP.NET Core refuse every request for it, which the behavior snapshot would catch, but this test catches the
/// mistake directly, by name, without needing to read a failing snapshot diff).
/// </summary>
public sealed class PermissionCoverageTests(ApiFixture fixture) : CatalogTestBase(fixture)
{
    /// <summary>
    /// Actions whose permission is decided at runtime, by the entity type in the URL, not by a static policy
    /// attribute (AdminRoutesController.Get/Change, AdminSeoController.Get/Save: docs/11 C-4 — the entity's own
    /// permission is enough) — RoutesTests/SeoTests cover their enforcement directly. /admin/me and the
    /// translations bundle are read by every signed-in admin, whatever they may edit. /admin/dashboard is
    /// field-level permission-aware INSIDE the service (docs/19 F2): the endpoint itself needs no single policy.
    /// </summary>
    private static readonly HashSet<string> DynamicOrUniversal =
    [
        "GET /api/v1/admin/routes/{type}/{id:int}",
        "PUT /api/v1/admin/routes/{type}/{id:int}",
        "GET /api/v1/admin/seo/{type}/{id:int}",
        "PUT /api/v1/admin/seo/{type}/{id:int}",
        "GET /api/v1/admin/me",
        "GET /api/v1/admin/translations/bundle",
        "GET /api/v1/admin/dashboard",
    ];

    [Fact]
    public void EveryAdminAction_RequiresAKnownPermission()
    {
        var endpoints = Fixture.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => ("/" + (e.RoutePattern.RawText ?? "").Trim('/')).StartsWith("/api/v1/admin", StringComparison.Ordinal));

        var missing = new List<string>();
        foreach (var endpoint in endpoints)
        {
            var template = "/" + (endpoint.RoutePattern.RawText ?? "").Trim('/');
            foreach (var method in endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [])
            {
                var key = $"{method} {template}";
                if (DynamicOrUniversal.Contains(key))
                {
                    continue;
                }

                var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                    .Select(a => a.Policy).Where(p => !string.IsNullOrEmpty(p)).ToList();
                var permissionPolicies = policies.Where(p => Permissions.All.Contains(p!)).ToList();
                if (permissionPolicies.Count == 0)
                {
                    missing.Add($"{key} has no known permission requirement (policies found: {(policies.Count == 0 ? "none" : string.Join(",", policies))})");
                }
            }
        }

        Assert.True(missing.Count == 0, "Admin actions without a known permission:\n" + string.Join('\n', missing));
    }

    [Fact]
    public async Task ARoleStillHoldingARetiredManagePermission_IsExpandedToItsGranularReplacements_NobodyLosesAccess()
    {
        Guid roleId;
        using (var scope = Fixture.Factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            var role = new ApplicationRole("LegacyCatalog" + Guid.NewGuid().ToString("N")[..8]);
            Assert.True((await roles.CreateAsync(role)).Succeeded);
            Assert.True((await roles.AddClaimAsync(role, new System.Security.Claims.Claim(Permissions.ClaimType, "Catalog.Manage"))).Succeeded);
            roleId = role.Id;
        }

        using (var scope = Fixture.Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(CancellationToken.None);
        }

        using (var scope = Fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
            var held = await db.RoleClaims.Where(c => c.RoleId == roleId && c.ClaimType == Permissions.ClaimType)
                .Select(c => c.ClaimValue!).ToListAsync();
            Assert.Equal(Permissions.PermissionsReplacedByManage["Catalog.Manage"].OrderBy(p => p, StringComparer.Ordinal),
                held.OrderBy(p => p, StringComparer.Ordinal));

            // Idempotent: running it again changes nothing further.
            using (var again = Fixture.Factory.Services.CreateScope())
            {
                await again.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(CancellationToken.None);
            }

            var heldAgain = await db.RoleClaims.AsNoTracking().Where(c => c.RoleId == roleId && c.ClaimType == Permissions.ClaimType)
                .Select(c => c.ClaimValue!).ToListAsync();
            Assert.Equal(held.OrderBy(p => p, StringComparer.Ordinal), heldAgain.OrderBy(p => p, StringComparer.Ordinal));
        }
    }
}
