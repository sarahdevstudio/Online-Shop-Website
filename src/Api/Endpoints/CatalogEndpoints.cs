using Shop.Api.Infrastructure;
using Shop.Application.Catalog;
using Shop.Application.Settings;

namespace Shop.Api.Endpoints;

internal static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this RouteGroupBuilder api)
    {
        var catalog = api.MapGroup("/catalog").WithTags("Catalog").AllowAnonymous();

        // Customer-facing store info (contact, shipping, available payment methods). No card details, no secrets.
        catalog.MapGet("/store-info", async (IStoreInfoService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(ct)));

        catalog.MapGet("/sitemap", async (ICatalogService service, CancellationToken ct) =>
            Results.Ok(await service.GetSitemapAsync(ct)));
    }
}
