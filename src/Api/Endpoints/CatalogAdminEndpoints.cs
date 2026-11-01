using Microsoft.AspNetCore.Mvc;
using Shop.Api.Infrastructure;
using Shop.Application.Catalog;
using Shop.Domain.Catalog;
using Shop.Domain.Security;

namespace Shop.Api.Endpoints;

internal static class CatalogAdminEndpoints
{
    public static void MapCatalogAdminEndpoints(this RouteGroupBuilder api)
    {
        var admin = api.MapGroup("/admin")
            .WithTags("Admin catalog")
            .RequireAuthorization(Permissions.CatalogManage);

        admin.MapDelete("/images/{id:int}", async (int id, ICatalogAdminService service, CancellationToken ct) =>
            (await service.DeleteImageAsync(id, ct)).ToHttpResult(Results.NoContent));
    }
}
