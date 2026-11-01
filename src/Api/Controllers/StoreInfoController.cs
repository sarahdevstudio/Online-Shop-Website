using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Settings;

namespace Shop.Api.Controllers;

// Customer-facing store info (contact, shipping, available payment methods). No card details, no secrets.
[Route("api/v1/catalog/store-info")]
[Tags("Catalog")]
[AllowAnonymous]
public sealed class StoreInfoController(IStoreInfoService storeInfo) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Http(Results.Ok(await storeInfo.GetAsync(ct)));
}
