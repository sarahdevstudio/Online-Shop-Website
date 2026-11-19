using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Shop.Api.Infrastructure;
using Shop.Application.Discounts;
using Shop.Application.Orders;

namespace Shop.Api.Controllers;

/// <summary>
/// Public cart API. Called only by the Next.js server (shared key); the cart token arrives explicitly in a header,
/// never as a cookie, so this controller is exempt from the cookie-CSRF filter (see <see cref="StorefrontApiAttribute"/>).
/// </summary>
[Route("api/v1/store/cart")]
[Tags("Store")]
[AllowAnonymous]
[StorefrontApi]
[StorefrontEndpoint]
[EnableRateLimiting(StoreEndpointsPolicies.Cart)]
public sealed class StoreCartController(ICartService carts) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Http(Results.Ok(await carts.GetAsync(CartToken(), ct)));

    [HttpGet("count")]
    public async Task<IActionResult> Count(CancellationToken ct) =>
        Http(Results.Ok(new CartCountDto(await carts.CountAsync(CartToken(), ct))));

    [HttpPost("items")]
    [ValidateRequest<AddCartItemRequest>]
    public async Task<IActionResult> AddItem([FromBody] AddCartItemRequest body, CancellationToken ct) =>
        Http(await carts.AddItemAsync(CartToken(), body, ct), Results.Ok);

    [HttpPut("items/{variantId:int}")]
    [ValidateRequest<UpdateCartItemRequest>]
    public async Task<IActionResult> SetQuantity(int variantId, [FromBody] UpdateCartItemRequest body, CancellationToken ct) =>
        Http(await carts.SetQuantityAsync(CartToken(), variantId, body.Quantity, ct), Results.Ok);

    [HttpDelete("items/{variantId:int}")]
    public async Task<IActionResult> RemoveItem(int variantId, CancellationToken ct) =>
        Http(await carts.SetQuantityAsync(CartToken(), variantId, 0, ct), Results.Ok);

    private string? CartToken() => Request.Headers[StoreEndpointsPolicies.CartTokenHeader].FirstOrDefault();
}

/// <summary>Its own controller (not <see cref="StoreCartController"/>) so it gets its own, stricter rate-limit policy:
/// guessing codes is an attack surface the rest of the cart is not.</summary>
[Route("api/v1/store/cart/discount")]
[Tags("Store")]
[AllowAnonymous]
[StorefrontApi]
[StorefrontEndpoint]
[EnableRateLimiting(StoreEndpointsPolicies.Discount)]
public sealed class StoreCartDiscountController(ICartService carts) : ApiControllerBase
{
    [HttpPost]
    [ValidateRequest<ApplyDiscountRequest>]
    public async Task<IActionResult> Apply([FromBody] ApplyDiscountRequest body, CancellationToken ct) =>
        Http(await carts.ApplyDiscountAsync(CartToken(), body.Code, ct), Results.Ok);

    [HttpDelete]
    public async Task<IActionResult> Remove(CancellationToken ct) =>
        Http(Results.Ok(await carts.RemoveDiscountAsync(CartToken(), ct)));

    private string? CartToken() => Request.Headers[StoreEndpointsPolicies.CartTokenHeader].FirstOrDefault();
}
