using Shop.Domain.Security;

namespace Shop.Api.Endpoints;

internal static class AdminEndpoints
{
    public static void MapAdminEndpoints(this RouteGroupBuilder api)
    {
        var admin = api.MapGroup("/admin")
            .WithTags("Admin")
            .RequireAuthorization(Permissions.SettingsManage);

        // Minimal permission-protected endpoint; real admin features arrive with their phases.
        admin.MapGet("/ping", () => Results.Ok(new { status = "ok" }));
    }
}
