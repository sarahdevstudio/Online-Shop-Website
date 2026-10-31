using Shop.Api.Infrastructure;
using Shop.Application.Admin;
using Shop.Domain.Security;

namespace Shop.Api.Endpoints;

internal static class AdminOperationsEndpoints
{
    public static void MapAdminOperationsEndpoints(this RouteGroupBuilder api)
    {
        var admin = api.MapGroup("/admin").WithTags("Admin");

        // Atomic +/- change without a row version: checkout changes stock all day, so a version check would fail constantly.
        admin.MapPost("/variants/{id:int}/stock-adjust", async (int id, StockAdjustRequest request, IStockAdminService service, CancellationToken ct) =>
                (await service.AdjustAsync(id, request, ct)).ToHttpResult(Results.Ok))
            .RequireAuthorization(Permissions.CatalogManage);
        admin.MapGet("/variants/{id:int}/stock-history", async (int id, IStockAdminService service, CancellationToken ct) =>
                (await service.HistoryAsync(id, ct)).ToHttpResult(Results.Ok))
            .RequireAuthorization(Permissions.CatalogManage);
    }
}
