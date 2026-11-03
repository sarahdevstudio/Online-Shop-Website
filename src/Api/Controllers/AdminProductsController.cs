using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Api.Infrastructure;
using Shop.Application.Catalog;
using Shop.Domain.Catalog;
using Shop.Domain.Security;

namespace Shop.Api.Controllers;

[Route("api/v1/admin/products")]
[Tags("Admin catalog")]
[Authorize(Policy = Permissions.CatalogManage)]
public sealed class AdminProductsController(ICatalogAdminService catalog) : ApiControllerBase
{
    // Query binding keeps the minimal-API rules: repeated values join with commas, empty is not absent, enum names
    // are case-sensitive, numbers are accepted (see RequestBinding.QueryBinding).
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? q,
        [FromQuery][ModelBinder(typeof(DocumentationOnlyModelBinder))] ProductStatus? status,
        [FromQuery][ModelBinder(typeof(DocumentationOnlyModelBinder))] int? page,
        CancellationToken ct)
    {
        if (!QueryBinding.TryGet(Request.Query, "page", out var pageValue) ||
            !QueryBinding.TryGetEnum<ProductStatus>(Request.Query, "status", out var statusValue))
        {
            return BadRequest();
        }

        var qValue = QueryBinding.GetString(Request.Query, "q");
        return Http(Results.Ok(await catalog.GetProductsAsync(qValue, statusValue, pageValue ?? 1, ct)));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        Http(await catalog.GetProductAsync(id, ct), Results.Ok);

    [HttpPost]
    [ValidateRequest<ProductRequest>]
    public async Task<IActionResult> Create([FromBody] ProductRequest request, CancellationToken ct) =>
        Http(await catalog.CreateProductAsync(request, ct), p => Results.Created($"/api/v1/admin/products/{p.Id}", p));

    [HttpPut("{id:int}")]
    [ValidateRequest<ProductRequest>]
    public async Task<IActionResult> Update(int id, [FromBody] ProductRequest request, CancellationToken ct) =>
        Http(await catalog.UpdateProductAsync(id, request, ct), Results.Ok);

    [HttpPost("{id:int}/variants")]
    [ValidateRequest<VariantRequest>]
    public async Task<IActionResult> AddVariant(int id, [FromBody] VariantRequest request, CancellationToken ct) =>
        Http(await catalog.AddVariantAsync(id, request, ct), v => Results.Created($"/api/v1/admin/products/{id}", v));

    [HttpPut("{id:int}/attributes")]
    [ValidateRequest<ProductAttributesRequest>]
    public async Task<IActionResult> SetAttributes(int id, [FromBody] ProductAttributesRequest request, CancellationToken ct) =>
        Http(await catalog.SetAttributesAsync(id, request, ct), Results.Ok);

    // Multipart upload. No framework antiforgery middleware is registered, so the API-wide X-XSRF-TOKEN filter
    // (CsrfFilter) is the only CSRF check, same as before.
    [HttpPost("{id:int}/images")]
    [RequireMultipartForm]
    public async Task<IActionResult> AddImage(int id, [Required] IFormFile file, [FromForm] string? alt, CancellationToken ct)
    {
        if (file is null)
        {
            return Http(Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: ProblemText.MalformedRequest));
        }

        await using var stream = file.OpenReadStream();
        var upload = new ImageUpload(stream, file.FileName, file.ContentType, file.Length, alt);
        return Http(await catalog.AddImageAsync(id, upload, ct), i => Results.Created($"/api/v1/admin/products/{id}", i));
    }

    [HttpPut("{id:int}/images/order")]
    [ValidateRequest<ImageOrderRequest>]
    public async Task<IActionResult> ReorderImages(int id, [FromBody] ImageOrderRequest request, CancellationToken ct) =>
        Http(await catalog.ReorderImagesAsync(id, request, ct), Results.Ok);
}
