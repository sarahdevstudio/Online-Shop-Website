using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Shop.Api.Infrastructure;
using Shop.Application.Orders;

namespace Shop.Api.Controllers;

/// <summary>
/// Public checkout / guest-order API. Called only by the Next.js server (shared key); order tokens arrive explicitly
/// in the path, never as a cookie, so this controller is exempt from the cookie-CSRF filter.
/// </summary>
[Route("api/v1/store/orders")]
[Tags("Store")]
[AllowAnonymous]
[StorefrontApi]
[StorefrontEndpoint]
public sealed class StoreOrdersController(IOrderService orders) : ApiControllerBase
{
    [HttpPost]
    [ValidateRequest<PlaceOrderRequest>]
    [EnableRateLimiting(StoreEndpointsPolicies.Order)]
    public async Task<IActionResult> Place([FromBody] PlaceOrderRequest body, CancellationToken ct)
    {
        if (!Guid.TryParse(Request.Headers[StoreEndpointsPolicies.IdempotencyKeyHeader].ToString(), out var key) || key == Guid.Empty)
        {
            return Http(Results.ValidationProblem(
                new Dictionary<string, string[]> { [StoreEndpointsPolicies.IdempotencyKeyHeader] = ["A GUID idempotency key is required."] }));
        }

        var cartToken = Request.Headers[StoreEndpointsPolicies.CartTokenHeader].FirstOrDefault();
        var context = new PlaceOrderContext(cartToken, key, ClientIp.From(HttpContext));
        var result = await orders.PlaceAsync(body, context, ct);

        // 201 for a new order, 200 when the same key was already used (a double submit).
        return result.IsSuccess
            ? Http(Results.Json(result.Value, statusCode: result.Value.Replayed ? StatusCodes.Status200OK : StatusCodes.Status201Created))
            : Http(result.Error!);
    }

    [HttpGet("{token:length(43)}")]
    [EnableRateLimiting(StoreEndpointsPolicies.Lookup)]
    public async Task<IActionResult> Get(string token, CancellationToken ct) =>
        Http(await orders.GetAsync(token, ct), Results.Ok);

    [HttpPost("{token:length(43)}/transfer-reference")]
    [ValidateRequest<TransferReferenceRequest>]
    [EnableRateLimiting(StoreEndpointsPolicies.Lookup)]
    public async Task<IActionResult> SubmitTransferReference(string token, [FromBody] TransferReferenceRequest body, CancellationToken ct) =>
        Http(await orders.SubmitTransferReferenceAsync(token, body, ct), Results.Ok);
}
