using Shop.Api.Infrastructure;
using Shop.Application.Content;

namespace Shop.Api.Endpoints;

internal static class ContentEndpoints
{
    public static void MapContentEndpoints(this RouteGroupBuilder api)
    {
        // Public: the storefront renders the Markdown (raw HTML is never rendered there).
        var pages = api.MapGroup("/catalog/pages").WithTags("Content").AllowAnonymous();
        pages.MapGet("", async (IContentService service, CancellationToken ct) => Results.Ok(await service.ListAsync(ct)));
        pages.MapGet("/{slug}", async (string slug, IContentService service, CancellationToken ct) =>
            (await service.GetAsync(slug, ct)).ToHttpResult(Results.Ok));
    }
}
