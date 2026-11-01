using Shop.Api.Infrastructure;
using Shop.Application.Blog;

namespace Shop.Api.Endpoints;

internal static class BlogEndpoints
{
    public static void MapBlogEndpoints(this RouteGroupBuilder api)
    {
        // Public: published posts only. The storefront renders the Markdown (raw HTML is never rendered there).
        var blog = api.MapGroup("/catalog/blog").WithTags("Blog").AllowAnonymous();
        blog.MapGet("", async (IBlogService service, CancellationToken ct, int page = 1) => Results.Ok(await service.ListAsync(page, ct)));
        blog.MapGet("/sitemap", async (IBlogService service, CancellationToken ct) => Results.Ok(await service.SitemapAsync(ct)));
        blog.MapGet("/{slug}", async (string slug, IBlogService service, CancellationToken ct) =>
            (await service.GetAsync(slug, ct)).ToHttpResult(Results.Ok));
    }
}
