using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Api.Infrastructure;
using Shop.Application.Catalog.Transfer;
using Shop.Domain.Security;

namespace Shop.Api.Controllers;

// Export is a GET (nothing changes except an audit row); the two import calls are multipart POSTs, so the API-wide
// X-XSRF-TOKEN filter is their CSRF check. The file is size-limited here and again while it is read.
[Route("api/v1/admin/catalog")]
[Tags("Admin catalog import and export")]
[Authorize]
public sealed class AdminCatalogTransferController(ICatalogTransferService transfer) : ApiControllerBase
{
    private const long RequestLimit = TableLimits.MaxFileBytes + 64 * 1024;

    [HttpGet("export")]
    [Authorize(Policy = Permissions.CatalogExport)]
    public async Task<IActionResult> Export([FromQuery] string? format, CancellationToken ct)
    {
        var file = await transfer.ExportAsync(format ?? "csv", ct);
        return Http(Results.File(file.Content, file.ContentType, file.FileName));
    }

    [HttpPost("import/preview")]
    [Authorize(Policy = Permissions.CatalogImport)]
    [RequireMultipartForm]
    [RequestSizeLimit(RequestLimit)]
    public async Task<IActionResult> Preview([Required] IFormFile file, CancellationToken ct)
    {
        if (file is null)
        {
            return Http(Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: ProblemText.MalformedRequest));
        }

        await using var stream = file.OpenReadStream();
        return Http(await transfer.PreviewAsync(stream, file.FileName, ct), Results.Ok);
    }

    [HttpPost("import/apply")]
    [Authorize(Policy = Permissions.CatalogImport)]
    [RequireMultipartForm]
    [RequestSizeLimit(RequestLimit)]
    public async Task<IActionResult> Apply([Required] IFormFile file, CancellationToken ct)
    {
        if (file is null)
        {
            return Http(Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: ProblemText.MalformedRequest));
        }

        await using var stream = file.OpenReadStream();
        return Http(await transfer.ApplyAsync(stream, file.FileName, ct), Results.Ok);
    }
}
