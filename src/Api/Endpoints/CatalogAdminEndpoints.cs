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

        admin.MapGet("/categories", async (ICatalogAdminService service, CancellationToken ct) =>
            Results.Ok(await service.GetCategoriesAsync(ct)));
        admin.MapPost("/categories", async (CategoryRequest request, ICatalogAdminService service, CancellationToken ct) =>
                (await service.CreateCategoryAsync(request, ct)).ToHttpResult(c => Results.Created($"/api/v1/admin/categories/{c.Id}", c)))
            .WithValidation<CategoryRequest>();
        admin.MapPut("/categories/{id:int}", async (int id, CategoryRequest request, ICatalogAdminService service, CancellationToken ct) =>
                (await service.UpdateCategoryAsync(id, request, ct)).ToHttpResult(Results.Ok))
            .WithValidation<CategoryRequest>();
        admin.MapDelete("/categories/{id:int}", async (int id, ICatalogAdminService service, CancellationToken ct) =>
            (await service.DeleteCategoryAsync(id, ct)).ToHttpResult(Results.NoContent));

        admin.MapGet("/brands", async (ICatalogAdminService service, CancellationToken ct) =>
            Results.Ok(await service.GetBrandsAsync(ct)));
        admin.MapPost("/brands", async (BrandRequest request, ICatalogAdminService service, CancellationToken ct) =>
                (await service.CreateBrandAsync(request, ct)).ToHttpResult(b => Results.Created($"/api/v1/admin/brands/{b.Id}", b)))
            .WithValidation<BrandRequest>();
        admin.MapPut("/brands/{id:int}", async (int id, BrandRequest request, ICatalogAdminService service, CancellationToken ct) =>
                (await service.UpdateBrandAsync(id, request, ct)).ToHttpResult(Results.Ok))
            .WithValidation<BrandRequest>();
        admin.MapDelete("/brands/{id:int}", async (int id, ICatalogAdminService service, CancellationToken ct) =>
            (await service.DeleteBrandAsync(id, ct)).ToHttpResult(Results.NoContent));

        admin.MapGet("/attributes", async (ICatalogAdminService service, CancellationToken ct) =>
            Results.Ok(await service.GetAttributesAsync(ct)));
        admin.MapPost("/attributes/{id:int}/options", async (int id, AttributeOptionRequest request, ICatalogAdminService service, CancellationToken ct) =>
                (await service.AddAttributeOptionAsync(id, request, ct)).ToHttpResult(o => Results.Created($"/api/v1/admin/attributes/{id}", o)))
            .WithValidation<AttributeOptionRequest>();

        admin.MapGet("/products", async (string? q, ProductStatus? status, int? page, ICatalogAdminService service, CancellationToken ct) =>
            Results.Ok(await service.GetProductsAsync(q, status, page ?? 1, ct)));
        admin.MapGet("/products/{id:int}", async (int id, ICatalogAdminService service, CancellationToken ct) =>
            (await service.GetProductAsync(id, ct)).ToHttpResult(Results.Ok));
        admin.MapPost("/products", async (ProductRequest request, ICatalogAdminService service, CancellationToken ct) =>
                (await service.CreateProductAsync(request, ct)).ToHttpResult(p => Results.Created($"/api/v1/admin/products/{p.Id}", p)))
            .WithValidation<ProductRequest>();
        admin.MapPut("/products/{id:int}", async (int id, ProductRequest request, ICatalogAdminService service, CancellationToken ct) =>
                (await service.UpdateProductAsync(id, request, ct)).ToHttpResult(Results.Ok))
            .WithValidation<ProductRequest>();

        admin.MapPost("/products/{id:int}/variants", async (int id, VariantRequest request, ICatalogAdminService service, CancellationToken ct) =>
                (await service.AddVariantAsync(id, request, ct)).ToHttpResult(v => Results.Created($"/api/v1/admin/products/{id}", v)))
            .WithValidation<VariantRequest>();
        admin.MapPut("/variants/{id:int}", async (int id, UpdateVariantRequest request, ICatalogAdminService service, CancellationToken ct) =>
                (await service.UpdateVariantAsync(id, request, ct)).ToHttpResult(Results.Ok))
            .WithValidation<UpdateVariantRequest>();

        admin.MapPut("/products/{id:int}/attributes", async (int id, ProductAttributesRequest request, ICatalogAdminService service, CancellationToken ct) =>
                (await service.SetAttributesAsync(id, request, ct)).ToHttpResult(Results.Ok))
            .WithValidation<ProductAttributesRequest>();

        // Multipart upload. The framework's form antiforgery is replaced by the API-wide X-XSRF-TOKEN filter.
        admin.MapPost("/products/{id:int}/images", async (int id, IFormFile file, [FromForm] string? alt, ICatalogAdminService service, CancellationToken ct) =>
            {
                await using var stream = file.OpenReadStream();
                var upload = new ImageUpload(stream, file.FileName, file.ContentType, file.Length, alt);
                return (await service.AddImageAsync(id, upload, ct)).ToHttpResult(i => Results.Created($"/api/v1/admin/products/{id}", i));
            })
            .DisableAntiforgery();
        admin.MapPut("/products/{id:int}/images/order", async (int id, ImageOrderRequest request, ICatalogAdminService service, CancellationToken ct) =>
                (await service.ReorderImagesAsync(id, request, ct)).ToHttpResult(Results.Ok))
            .WithValidation<ImageOrderRequest>();
        admin.MapDelete("/images/{id:int}", async (int id, ICatalogAdminService service, CancellationToken ct) =>
            (await service.DeleteImageAsync(id, ct)).ToHttpResult(Results.NoContent));
    }
}
