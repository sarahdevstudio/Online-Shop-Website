using Shop.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Common;
using Shop.Application.Orders;
using Shop.Domain.Common;
using Shop.Domain.Orders;
using Shop.Domain.Security;

namespace Shop.Api.Controllers;

[Route("api/v1/admin/orders")]
[Tags("Admin orders")]
[Authorize]
public sealed class AdminOrdersController(IAdminOrderService orders, IOrderService placement, ICurrentUser currentUser) : ApiControllerBase
{
    // A phone order (G3): same IOrderService.PlaceAsync as the shop checkout (pricing, atomic stock decrement,
    // idempotency, discount resolution - no manual price override), with the draft cart built via
    // AdminDraftOrderController. Source=Admin skips only the per-mobile/per-IP unpaid-order limit; everything else
    // (price_changed on a stale quote, stock shortages, 409 conflicts) behaves exactly like a guest order.
    [HttpPost]
    [Authorize(Policy = Permissions.OrdersCreate)]
    [ValidateRequest<PlaceOrderRequest>]
    public async Task<IActionResult> Place(
        [FromQuery] string? cartToken, [FromQuery] string? idempotencyKey,
        [FromBody] PlaceOrderRequest body, CancellationToken ct)
    {
        if (!Guid.TryParse(idempotencyKey, out var key) || key == Guid.Empty)
        {
            return Http(Results.ValidationProblem(
                Messages.Resolve(new Dictionary<string, string[]> { ["idempotencyKey"] = [Msg.Of("api.validation.idempotency-key-required")] }),
                title: ProblemText.BadRequest));
        }

        var context = new PlaceOrderContext(cartToken, key, ClientIp.From(HttpContext), OrderSource.Admin, currentUser.UserId);
        var result = await placement.PlaceAsync(body, context, ct);
        return result.IsSuccess
            ? Http(Results.Json(result.Value, statusCode: result.Value.Replayed ? StatusCodes.Status200OK : StatusCodes.Status201Created))
            : Http(result.Error!);
    }

    // Enum filters arrive as camelCase strings, like everywhere else in the JSON API (case-sensitive-numeric-rejecting parse).
    // The printable invoice/packing-slip page (storefront/src/app/admin/print) reads an order through this same
    // Get action; Orders.Print only gates whether its button/link is shown, since there is no separate print endpoint.
    [HttpGet]
    [Authorize(Policy = Permissions.OrdersView)]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] string? paymentStatus,
        [FromQuery] string? q,
        [FromQuery][ModelBinder(typeof(DocumentationOnlyModelBinder))] int? page,
        CancellationToken ct)
    {
        if (!QueryBinding.TryGet(Request.Query, "page", out var pageValue))
        {
            return BadRequest();
        }

        if (!TryParseFilter<OrderStatus>(status, out var statusFilter) || !TryParseFilter<PaymentStatus>(paymentStatus, out var paymentFilter))
        {
            return Http(Results.ValidationProblem(Messages.Resolve(new Dictionary<string, string[]> { ["filter"] = ["api.validation.order-filter"] }), title: ProblemText.BadRequest));
        }

        return Http(Results.Ok(await orders.ListAsync(statusFilter, paymentFilter, q, pageValue ?? 1, ct)));
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = Permissions.OrdersView)]
    public async Task<IActionResult> Get(int id, CancellationToken ct) =>
        Http(await orders.GetAsync(id, ct), Results.Ok);

    [HttpPost("{id:int}/confirm-payment")]
    [Authorize(Policy = Permissions.OrdersConfirmPayment)]
    public async Task<IActionResult> ConfirmPayment(int id, CancellationToken ct) =>
        Http(await orders.ConfirmPaymentAsync(id, ct), Results.Ok);

    [HttpPost("{id:int}/status")]
    [Authorize(Policy = Permissions.OrdersUpdate)]
    public async Task<IActionResult> SetStatus(int id, [FromBody] SetOrderStatusRequest body, CancellationToken ct) =>
        Http(await orders.SetStatusAsync(id, body.Status, ct), Results.Ok);

    [HttpPost("{id:int}/cancel")]
    [Authorize(Policy = Permissions.OrdersCancel)]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct) =>
        Http(await orders.CancelAsync(id, ct), Results.Ok);

    private static bool TryParseFilter<T>(string? value, out T? result) where T : struct, Enum
    {
        result = null;
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        if (Enum.TryParse<T>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed) && !int.TryParse(value, out _))
        {
            result = parsed;
            return true;
        }

        return false;
    }
}
