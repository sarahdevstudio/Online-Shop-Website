using Shop.Api.Infrastructure;
using Shop.Application.Accounts;
using Shop.Application.Common;

namespace Shop.Api.Endpoints;

internal static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this RouteGroupBuilder api)
    {
        var customer = api.MapGroup("/customer")
            .WithTags("Customer")
            .RequireAuthorization(ApiServiceCollectionExtensions.CustomerPolicy);

        // The profile is always the caller's own: the id comes from the auth cookie, never from the request.
        customer.MapGet("/profile", async (ICurrentUser currentUser, IAccountService accounts, CancellationToken ct) =>
            (await accounts.GetCustomerProfileAsync(currentUser.UserId!.Value, ct)).ToHttpResult(Results.Ok));
    }
}
