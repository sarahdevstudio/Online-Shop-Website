using Shop.Api.Infrastructure;
using Shop.Application.Catalog;
using Shop.Application.Settings;

namespace Shop.Api.Endpoints;

internal static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this RouteGroupBuilder api)
    {
        var catalog = api.MapGroup("/catalog").WithTags("Catalog").AllowAnonymous();

        catalog.MapGet("/sitemap", async (ICatalogService service, CancellationToken ct) =>
            Results.Ok(await service.GetSitemapAsync(ct)));
    }
}
