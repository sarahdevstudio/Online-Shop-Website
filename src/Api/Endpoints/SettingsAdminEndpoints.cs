using Shop.Api.Infrastructure;
using Shop.Application.Settings;
using Shop.Domain.Security;

namespace Shop.Api.Endpoints;

internal static class SettingsAdminEndpoints
{
    public static void MapSettingsAdminEndpoints(this RouteGroupBuilder api)
    {
        // Settings.Manage is its own permission (not Catalog.Manage). Writes need the antiforgery token like every admin call.
        var settings = api.MapGroup("/admin/settings")
            .WithTags("Admin settings")
            .RequireAuthorization(Permissions.SettingsManage);

        settings.MapGet("", async (IAdminSettingsService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(ct)));
        settings.MapPut("", async (UpdateStoreSettingsRequest request, IAdminSettingsService service, CancellationToken ct) =>
            (await service.UpdateAsync(request, ct)).ToHttpResult(Results.Ok));
        settings.MapGet("/audit", async (int? page, IAdminSettingsService service, CancellationToken ct) =>
            Results.Ok(await service.GetAuditAsync(page ?? 1, ct)));
    }
}
