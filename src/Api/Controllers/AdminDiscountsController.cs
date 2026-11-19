using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Discounts;
using Shop.Domain.Security;

namespace Shop.Api.Controllers;

// CSRF like every admin write, 409 on a stale row version.
[Route("api/v1/admin/discounts")]
[Tags("Admin discounts")]
[Authorize]
public sealed class AdminDiscountsController(IAdminDiscountService discounts) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.DiscountsView)]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Http(Results.Ok(await discounts.ListAsync(ct)));

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.DiscountsView)]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        Http(await discounts.GetAsync(id, ct), Results.Ok);

    [HttpPost]
    [Authorize(Policy = Permissions.DiscountsCreate)]
    public async Task<IActionResult> Create([FromBody] CreateDiscountCodeRequest request, CancellationToken ct) =>
        Http(await discounts.CreateAsync(request, ct), d => Results.Created($"/api/v1/admin/discounts/{d.Id}", d));

    [HttpPut("{id:int}")]
    [Authorize(Policy = Permissions.DiscountsUpdate)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDiscountCodeRequest request, CancellationToken ct) =>
        Http(await discounts.UpdateAsync(id, request, ct), Results.Ok);

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Permissions.DiscountsDelete)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        Http(await discounts.DeleteAsync(id, ct), Results.NoContent);

    [HttpGet("{id:int}/audit")]
    [Authorize(Policy = Permissions.DiscountsView)]
    public async Task<IActionResult> Audit(int id, CancellationToken ct) =>
        Http(await discounts.AuditAsync(id, ct), Results.Ok);
}
