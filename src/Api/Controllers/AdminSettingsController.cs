using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Application.Common;
using Shop.Application.Settings;
using Shop.Domain.Security;

namespace Shop.Api.Controllers;

// Writes need the antiforgery token like every admin call. Update needs Settings.Update; a request that also
// changes a payment field (CardNumber, CardHolderName, BankName, CardToCardEnabled) additionally needs
// Settings.Payment, checked here since it is one PUT for the whole settings row, not a separate endpoint.
[Route("api/v1/admin/settings")]
[Tags("Admin settings")]
[Authorize]
public sealed class AdminSettingsController(IAdminSettingsService settings, ICurrentUser currentUser) : ApiControllerBase
{
    [HttpGet]
    [Authorize(Policy = Permissions.SettingsView)]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        Http(Results.Ok(await settings.GetAsync(ct)));

    [HttpPut]
    [Authorize(Policy = Permissions.SettingsUpdate)]
    public async Task<IActionResult> Update([FromBody] UpdateStoreSettingsRequest request, CancellationToken ct)
    {
        if (!currentUser.HasPermission(Permissions.SettingsPayment))
        {
            var current = (await settings.GetAsync(ct)).Settings;
            var changesPayment = current.CardNumber != request.Settings.CardNumber
                || current.CardHolderName != request.Settings.CardHolderName
                || current.BankName != request.Settings.BankName
                || current.CardToCardEnabled != request.Settings.CardToCardEnabled;
            if (changesPayment)
            {
                return Http(Error.Forbidden("forbidden", "api.error.forbidden"));
            }
        }

        return Http(await settings.UpdateAsync(request, ct), Results.Ok);
    }

    [HttpGet("audit")]
    [Authorize(Policy = Permissions.SettingsView)]
    public async Task<IActionResult> Audit([FromQuery] int? page, CancellationToken ct) =>
        Http(Results.Ok(await settings.GetAuditAsync(page ?? 1, ct)));
}
