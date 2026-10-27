using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Shop.Api.Infrastructure;
using Shop.Application.Accounts;
using Shop.Application.Admin;
using Shop.Application.Common;

namespace Shop.Api.Controllers;

[Route("api/v1/auth")]
[Tags("Auth")]
public sealed class AuthController(IAccountService accounts, ICurrentUser currentUser, IAntiforgery antiforgery) : ApiControllerBase
{
    // Unsafe verbs require the antiforgery header. The token is bound to the current identity, so
    // clients must fetch a fresh one after logging in or out.
    [HttpGet("csrf")]
    [AllowAnonymous]
    public IActionResult Csrf()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Http(Results.Ok(new CsrfTokenDto("X-XSRF-TOKEN", tokens.RequestToken!)));
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(ApiServiceCollectionExtensions.AuthRateLimitPolicy)]
    [ValidateRequest<RegisterCustomerRequest>]
    public async Task<IActionResult> Register([FromBody] RegisterCustomerRequest request, CancellationToken ct) =>
        Http(await accounts.RegisterCustomerAsync(request, ct), account => Results.Created("/api/v1/auth/me", account));

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(ApiServiceCollectionExtensions.AuthRateLimitPolicy)]
    [ValidateRequest<LoginRequest>]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct) =>
        Http(await accounts.LoginAsync(request, ct), Results.Ok);

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await accounts.LogoutAsync(ct);
        return Http(Results.NoContent());
    }

    // Revokes every other session and keeps this one signed in on a fresh cookie.
    [HttpPost("change-password")]
    [Authorize]
    [EnableRateLimiting(ApiServiceCollectionExtensions.AuthRateLimitPolicy)]
    [ValidateRequest<ChangePasswordRequest>]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct) =>
        Http(await accounts.ChangePasswordAsync(currentUser.UserId!.Value, request, ct), Results.NoContent);

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct) =>
        Http(await accounts.GetAccountAsync(currentUser.UserId!.Value, ct), Results.Ok);

    private sealed record CsrfTokenDto(string HeaderName, string Token);
}
