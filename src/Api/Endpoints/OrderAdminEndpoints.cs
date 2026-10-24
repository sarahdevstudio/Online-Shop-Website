using Shop.Api.Infrastructure;
using Shop.Application.Orders;
using Shop.Domain.Orders;
using Shop.Domain.Security;

namespace Shop.Api.Endpoints;

internal static class OrderAdminEndpoints
{
    public static void MapOrderAdminEndpoints(this RouteGroupBuilder api)
    {
        var orders = api.MapGroup("/admin/orders")
            .WithTags("Admin orders")
            .RequireAuthorization(Permissions.OrdersManage);

        // Enum filters arrive as camelCase strings, like everywhere else in the JSON API (minimal-API enum binding is case-sensitive).
        orders.MapGet("", async (string? status, string? paymentStatus, string? q, int? page, IAdminOrderService service, CancellationToken ct) =>
        {
            if (!TryParse<OrderStatus>(status, out var statusFilter) || !TryParse<PaymentStatus>(paymentStatus, out var paymentFilter))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["filter"] = ["Unknown status or paymentStatus."] });
            }

            return Results.Ok(await service.ListAsync(statusFilter, paymentFilter, q, page ?? 1, ct));
        });
        orders.MapGet("/{id:int}", async (int id, IAdminOrderService service, CancellationToken ct) =>
            (await service.GetAsync(id, ct)).ToHttpResult(Results.Ok));
        orders.MapPost("/{id:int}/confirm-payment", async (int id, IAdminOrderService service, CancellationToken ct) =>
            (await service.ConfirmPaymentAsync(id, ct)).ToHttpResult(Results.Ok));
        orders.MapPost("/{id:int}/status", async (int id, SetOrderStatusRequest body, IAdminOrderService service, CancellationToken ct) =>
            (await service.SetStatusAsync(id, body.Status, ct)).ToHttpResult(Results.Ok));
        orders.MapPost("/{id:int}/cancel", async (int id, IAdminOrderService service, CancellationToken ct) =>
            (await service.CancelAsync(id, ct)).ToHttpResult(Results.Ok));
    }

    private static bool TryParse<T>(string? value, out T? result) where T : struct, Enum
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
