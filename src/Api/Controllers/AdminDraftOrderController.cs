using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Api.Infrastructure;
using Shop.Application.Discounts;
using Shop.Application.Orders;
using Shop.Domain.Security;

namespace Shop.Api.Controllers;

/// <summary>
/// A phone order's draft cart (G3): an admin builds it exactly like a shopper builds the shop cart - the same
/// <see cref="ICartService"/>, so pricing, stock checks and discount resolution never diverge - except the token is
/// carried as a query parameter (the admin session cookie is the auth, not a cart cookie) instead of the shop's
/// cart-token header, and every action needs Orders.Create.
/// </summary>
[Route("api/v1/admin/orders/draft")]
[Tags("Admin orders")]
[Authorize]
public sealed class AdminDraftOrderController(ICartService cart) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.OrdersCreate)]
    public async Task<IActionResult> Get([FromQuery] string? cartToken, CancellationToken ct) =>
        Http(Results.Ok(await cart.GetAsync(cartToken, ct)));

    [HttpPost("items")]
    [Authorize(Policy = Permissions.OrdersCreate)]
    [ValidateRequest<AddCartItemRequest>]
    public async Task<IActionResult> AddItem([FromQuery] string? cartToken, [FromBody] AddCartItemRequest body, CancellationToken ct) =>
        Http(await cart.AddItemAsync(cartToken, body, ct), Results.Ok);

    [HttpPut("items/{variantId:int}")]
    [Authorize(Policy = Permissions.OrdersCreate)]
    [ValidateRequest<UpdateCartItemRequest>]
    public async Task<IActionResult> SetQuantity([FromQuery] string? cartToken, int variantId, [FromBody] UpdateCartItemRequest body, CancellationToken ct) =>
        Http(await cart.SetQuantityAsync(cartToken, variantId, body.Quantity, ct), Results.Ok);

    [HttpPost("discount")]
    [Authorize(Policy = Permissions.OrdersCreate)]
    [ValidateRequest<ApplyDiscountRequest>]
    public async Task<IActionResult> ApplyDiscount([FromQuery] string? cartToken, [FromBody] ApplyDiscountRequest body, CancellationToken ct) =>
        Http(await cart.ApplyDiscountAsync(cartToken, body.Code, ct), Results.Ok);

    [HttpDelete("discount")]
    [Authorize(Policy = Permissions.OrdersCreate)]
    public async Task<IActionResult> RemoveDiscount([FromQuery] string? cartToken, CancellationToken ct) =>
        Http(Results.Ok(await cart.RemoveDiscountAsync(cartToken, ct)));
}
