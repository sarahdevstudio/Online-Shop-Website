using Shop.Api.Infrastructure;
using Shop.Application.Blog;
using Shop.Domain.Security;

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

        // Admin: Blog.Manage (its own permission), CSRF like every admin write, 409 on a stale row version.
        var admin = api.MapGroup("/admin/blog").WithTags("Admin blog").RequireAuthorization(Permissions.BlogManage);
        admin.MapGet("", async (IAdminBlogService service, CancellationToken ct) => Results.Ok(await service.ListAsync(ct)));
        admin.MapGet("/{id:int}", async (int id, IAdminBlogService service, CancellationToken ct) =>
            (await service.GetAsync(id, ct)).ToHttpResult(Results.Ok));
        admin.MapPost("", async (CreateBlogPostRequest request, IAdminBlogService service, CancellationToken ct) =>
            (await service.CreateAsync(request, ct)).ToHttpResult(post => Results.Created($"/api/v1/admin/blog/{post.Id}", post)));
        admin.MapPut("/{id:int}", async (int id, UpdateBlogPostRequest request, IAdminBlogService service, CancellationToken ct) =>
            (await service.UpdateAsync(id, request, ct)).ToHttpResult(Results.Ok));
        admin.MapDelete("/{id:int}", async (int id, IAdminBlogService service, CancellationToken ct) =>
            (await service.DeleteAsync(id, ct)).ToHttpResult(Results.NoContent));
        admin.MapGet("/{id:int}/audit", async (int id, IAdminBlogService service, CancellationToken ct) =>
            (await service.AuditAsync(id, ct)).ToHttpResult(Results.Ok));
    }
}
