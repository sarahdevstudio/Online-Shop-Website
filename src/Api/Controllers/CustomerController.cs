using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Api.Infrastructure;
using Shop.Application.Accounts;
using Shop.Application.Common;

namespace Shop.Api.Controllers;

[Route("api/v1/customer")]
[Tags("Customer")]
[Authorize(Policy = ApiServiceCollectionExtensions.CustomerPolicy)]
public sealed class CustomerController(IAccountService accounts, ICurrentUser currentUser) : ApiControllerBase
{
    // The profile is always the caller's own: the id comes from the auth cookie, never from the request.
    [HttpGet("profile")]
    public async Task<IActionResult> Profile(CancellationToken ct) =>
        Http(await accounts.GetCustomerProfileAsync(currentUser.UserId!.Value, ct), Results.Ok);
}
