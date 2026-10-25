using Shop.Api.Infrastructure;
using Shop.Application.Catalog;
using Shop.Application.Settings;

namespace Shop.Api.Endpoints;

internal static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this RouteGroupBuilder api)
    {
        var catalog = api.MapGroup("/catalog").WithTags("Catalog").AllowAnonymous();

        catalog.MapGet("/categories", async (ICatalogService service, CancellationToken ct) =>
            Results.Ok(await service.GetCategoryTreeAsync(ct)));

        catalog.MapGet("/categories/{slug}", async (string slug, ICatalogService service, CancellationToken ct) =>
            (await service.GetCategoryAsync(slug, ct)).ToHttpResult(Results.Ok));

        catalog.MapGet("/brands", async (ICatalogService service, CancellationToken ct) =>
            Results.Ok(await service.GetBrandsAsync(ct)));

        // ?category=&q=&brand=a,b&<attributeCode>=slug1,slug2&minPrice=&maxPrice=&inStock=&sort=&page=&pageSize=
        catalog.MapGet("/products", async (HttpRequest request, ICatalogService service, CancellationToken ct) =>
        {
            var query = ProductListQueryParser.Parse(
                request.Query.Select(pair => (pair.Key, (IReadOnlyList<string?>)pair.Value.ToArray())));
            if (!query.IsSuccess)
            {
                return query.Error!.ToProblem();
            }

            return (await service.GetProductsAsync(query.Value, ct)).ToHttpResult(Results.Ok);
        });

        catalog.MapGet("/products/{slug}", async (string slug, ICatalogService service, CancellationToken ct) =>
            (await service.GetProductAsync(slug, ct)).ToHttpResult(Results.Ok));

        // Customer-facing store info (contact, shipping, available payment methods). No card details, no secrets.
        catalog.MapGet("/store-info", async (IStoreInfoService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(ct)));

        catalog.MapGet("/sitemap", async (ICatalogService service, CancellationToken ct) =>
            Results.Ok(await service.GetSitemapAsync(ct)));
    }
}
