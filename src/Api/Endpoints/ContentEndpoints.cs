using Shop.Api.Infrastructure;
using Shop.Application.Content;
using Shop.Domain.Security;

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

        // Admin: Content.Manage (its own permission), CSRF like every admin write, 409 on a stale row version.
        var admin = api.MapGroup("/admin/content-pages").WithTags("Admin content").RequireAuthorization(Permissions.ContentManage);
        admin.MapGet("", async (IAdminContentService service, CancellationToken ct) => Results.Ok(await service.ListAsync(ct)));
        admin.MapGet("/{slug}", async (string slug, IAdminContentService service, CancellationToken ct) =>
            (await service.GetAsync(slug, ct)).ToHttpResult(Results.Ok));
        admin.MapPut("/{slug}", async (string slug, UpdateContentPageRequest request, IAdminContentService service, CancellationToken ct) =>
            (await service.UpdateAsync(slug, request, ct)).ToHttpResult(Results.Ok));
        admin.MapGet("/{slug}/audit", async (string slug, IAdminContentService service, CancellationToken ct) =>
            (await service.AuditAsync(slug, ct)).ToHttpResult(Results.Ok));
    }
}
