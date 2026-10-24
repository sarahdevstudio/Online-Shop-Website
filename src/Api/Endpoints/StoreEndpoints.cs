using Shop.Api.Infrastructure;
using Shop.Application.Orders;

namespace Shop.Api.Endpoints;

/// <summary>
/// Public cart / checkout / guest-order API. Called only by the Next.js server (shared key); the cart and order tokens
/// arrive explicitly in headers and paths, never as cookies, so these routes are exempt from the cookie-CSRF filter.
/// </summary>
internal static class StoreEndpoints
{
    public const string CartTokenHeader = "X-Cart-Token";
    public const string IdempotencyKeyHeader = "Idempotency-Key";
    public const string CartPolicy = "store-cart";
    public const string OrderPolicy = "store-order";
    public const string LookupPolicy = "store-lookup";

    public static void MapStoreEndpoints(this RouteGroupBuilder api)
    {
        var store = api.MapGroup("/store")
            .WithTags("Store")
            .AllowAnonymous()
            .WithMetadata(StorefrontEndpointMetadata.Instance)
            .AddEndpointFilter<StorefrontKeyFilter>();

        var cart = store.MapGroup("/cart").RequireRateLimiting(CartPolicy);
        cart.MapGet("", async (HttpRequest request, ICartService carts, CancellationToken ct) =>
            Results.Ok(await carts.GetAsync(CartToken(request), ct)));
        cart.MapGet("/count", async (HttpRequest request, ICartService carts, CancellationToken ct) =>
            Results.Ok(new CartCountDto(await carts.CountAsync(CartToken(request), ct))));
        cart.MapPost("/items", async (HttpRequest request, AddCartItemRequest body, ICartService carts, CancellationToken ct) =>
                (await carts.AddItemAsync(CartToken(request), body, ct)).ToHttpResult(Results.Ok))
            .WithValidation<AddCartItemRequest>();
        cart.MapPut("/items/{variantId:int}", async (HttpRequest request, int variantId, UpdateCartItemRequest body, ICartService carts, CancellationToken ct) =>
                (await carts.SetQuantityAsync(CartToken(request), variantId, body.Quantity, ct)).ToHttpResult(Results.Ok))
            .WithValidation<UpdateCartItemRequest>();
        cart.MapDelete("/items/{variantId:int}", async (HttpRequest request, int variantId, ICartService carts, CancellationToken ct) =>
            (await carts.SetQuantityAsync(CartToken(request), variantId, 0, ct)).ToHttpResult(Results.Ok));

        store.MapPost("/orders", async (HttpContext http, PlaceOrderRequest body, IOrderService orders, CancellationToken ct) =>
            {
                if (!Guid.TryParse(http.Request.Headers[IdempotencyKeyHeader].ToString(), out var key) || key == Guid.Empty)
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]> { [IdempotencyKeyHeader] = ["A GUID idempotency key is required."] });
                }

                var context = new PlaceOrderContext(CartToken(http.Request), key, ClientIp.From(http));
                var result = await orders.PlaceAsync(body, context, ct);

                // 201 for a new order, 200 when the same key was already used (a double submit).
                return result.IsSuccess
                    ? Results.Json(result.Value, statusCode: result.Value.Replayed ? StatusCodes.Status200OK : StatusCodes.Status201Created)
                    : result.Error!.ToProblem();
            })
            .WithValidation<PlaceOrderRequest>()
            .RequireRateLimiting(OrderPolicy);

        var order = store.MapGroup("/orders/{token:length(43)}").RequireRateLimiting(LookupPolicy);
        order.MapGet("", async (string token, IOrderService orders, CancellationToken ct) =>
            (await orders.GetAsync(token, ct)).ToHttpResult(Results.Ok));
        order.MapPost("/transfer-reference", async (string token, TransferReferenceRequest body, IOrderService orders, CancellationToken ct) =>
                (await orders.SubmitTransferReferenceAsync(token, body, ct)).ToHttpResult(Results.Ok))
            .WithValidation<TransferReferenceRequest>();
    }

    private static string? CartToken(HttpRequest request) =>
        request.Headers[CartTokenHeader].FirstOrDefault();
}
