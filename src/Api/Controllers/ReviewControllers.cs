using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Shop.Api.Infrastructure;
using Shop.Application.Reviews;
using Shop.Domain.Reviews;
using Shop.Domain.Security;

namespace Shop.Api.Controllers;

/// <summary>Guests submit a review (called by the storefront server only, like checkout). It is held for moderation: nothing is public yet.</summary>
[Route("api/v1/store/reviews")]
[Tags("Store")]
[AllowAnonymous]
[StorefrontApi]
[StorefrontEndpoint]
public sealed class StoreReviewsController(IReviewService reviews) : ApiControllerBase
{
    [HttpPost]
    [EnableRateLimiting(StoreEndpointsPolicies.Review)]
    public async Task<IActionResult> Submit([FromBody] SubmitReviewRequest request, CancellationToken ct) =>
        Http(await reviews.SubmitAsync(request, ct), () => Results.Json(new { received = true }, statusCode: StatusCodes.Status202Accepted));
}

/// <summary>The approved reviews of a product and their average (nothing else is ever public).</summary>
[Route("api/v1/catalog/reviews")]
[Tags("Catalog")]
[AllowAnonymous]
public sealed class CatalogReviewsController(IReviewService reviews) : ApiControllerBase
{
    [HttpGet("{slug}")]
    public async Task<IActionResult> Get(string slug, CancellationToken ct) => Http(await reviews.GetApprovedAsync(slug, ct), Results.Ok);
}

// Reviews.Manage is its own permission. CSRF like every admin write, 409 on a stale row version, every decision in the moderation log.
[Route("api/v1/admin/reviews")]
[Tags("Admin reviews")]
[Authorize(Policy = Permissions.ReviewsManage)]
public sealed class AdminReviewsController(IAdminReviewService reviews) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ReviewStatus? status, [FromQuery] int? page, CancellationToken ct) =>
        Http(Results.Ok(await reviews.ListAsync(status, page ?? 1, ct)));

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, [FromBody] ModerateReviewRequest request, CancellationToken ct) =>
        Http(await reviews.ApproveAsync(id, request, ct), Results.Ok);

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> Reject(int id, [FromBody] ModerateReviewRequest request, CancellationToken ct) =>
        Http(await reviews.RejectAsync(id, request, ct), Results.Ok);

    [HttpGet("audit")]
    public async Task<IActionResult> Audit([FromQuery] int? page, CancellationToken ct) =>
        Http(Results.Ok(await reviews.AuditAsync(page ?? 1, ct)));
}
