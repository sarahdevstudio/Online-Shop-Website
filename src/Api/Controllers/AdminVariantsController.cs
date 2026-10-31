using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Admin;
using Shop.Application.Catalog;
using Shop.Domain.Security;

namespace Shop.Api.Controllers;

[Route("api/v1/admin/variants")]
[Authorize(Policy = Permissions.CatalogManage)]
public sealed class AdminVariantsController(ICatalogAdminService catalog, IStockAdminService stock) : ApiControllerBase
{
    [HttpPut("{id:int}")]
    [Tags("Admin catalog")]
    [ValidateRequest<UpdateVariantRequest>]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateVariantRequest request, CancellationToken ct) =>
        Http(await catalog.UpdateVariantAsync(id, request, ct), Results.Ok);

    // Atomic +/- change without a row version: checkout changes stock all day, so a version check would fail constantly.
    [HttpPost("{id:int}/stock-adjust")]
    [Tags("Admin")]
    public async Task<IActionResult> StockAdjust(int id, [FromBody] StockAdjustRequest request, CancellationToken ct) =>
        Http(await stock.AdjustAsync(id, request, ct), Results.Ok);

    [HttpGet("{id:int}/stock-history")]
    [Tags("Admin")]
    public async Task<IActionResult> StockHistory(int id, CancellationToken ct) =>
        Http(await stock.HistoryAsync(id, ct), Results.Ok);
}
