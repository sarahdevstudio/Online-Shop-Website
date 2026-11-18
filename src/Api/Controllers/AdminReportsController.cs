using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shop.Api.Infrastructure;
using Shop.Application.Admin;
using Shop.Domain.Security;

namespace Shop.Api.Controllers;

/// <summary>Sales/profit reports (docs/19 F2). The admin picks the period (presets or a custom range) in Tehran time;
/// the storefront converts it to a UTC [from, to) range before calling this.</summary>
[Route("api/v1/admin/reports")]
[Tags("Admin reports")]
[Authorize(Policy = Permissions.ReportsView)]
public sealed class AdminReportsController(IReportsService reports) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct)
    {
        var fromUtc = DateTime.SpecifyKind(from, DateTimeKind.Utc);
        var toUtc = DateTime.SpecifyKind(to, DateTimeKind.Utc);
        if (toUtc <= fromUtc || (toUtc - fromUtc).TotalDays > 366)
        {
            return Http(Results.ValidationProblem(
                Messages.Resolve(new Dictionary<string, string[]> { ["to"] = ["api.validation.reports-range"] }),
                title: ProblemText.BadRequest));
        }

        return Http(Results.Ok(await reports.GetAsync(fromUtc, toUtc, ct)));
    }
}
