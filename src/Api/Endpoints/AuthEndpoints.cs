using Microsoft.AspNetCore.Antiforgery;
using Shop.Api.Infrastructure;
using Shop.Application.Accounts;
using Shop.Application.Common;

namespace Shop.Api.Endpoints;

internal static class AuthEndpoints
{
    public static void MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth").WithTags("Auth");

        // Unsafe verbs require the antiforgery header. The token is bound to the current identity, so
        // clients must fetch a fresh one after logging in or out.
        auth.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) =>
            {
                var tokens = antiforgery.GetAndStoreTokens(context);
                return Results.Ok(new CsrfTokenDto("X-XSRF-TOKEN", tokens.RequestToken!));
            })
            .AllowAnonymous();

        auth.MapPost("/register", async (RegisterCustomerRequest request, IAccountService accounts, CancellationToken ct) =>
            (await accounts.RegisterCustomerAsync(request, ct))
                .ToHttpResult(account => Results.Created("/api/v1/auth/me", account)))
            .WithValidation<RegisterCustomerRequest>()
            .RequireRateLimiting(ApiServiceCollectionExtensions.AuthRateLimitPolicy)
            .AllowAnonymous();

        auth.MapPost("/login", async (LoginRequest request, IAccountService accounts, CancellationToken ct) =>
            (await accounts.LoginAsync(request, ct)).ToHttpResult(Results.Ok))
            .WithValidation<LoginRequest>()
            .RequireRateLimiting(ApiServiceCollectionExtensions.AuthRateLimitPolicy)
            .AllowAnonymous();

        auth.MapPost("/logout", async (IAccountService accounts, CancellationToken ct) =>
            {
                await accounts.LogoutAsync(ct);
                return Results.NoContent();
            })
            .RequireAuthorization();

        auth.MapGet("/me", async (ICurrentUser currentUser, IAccountService accounts, CancellationToken ct) =>
            (await accounts.GetAccountAsync(currentUser.UserId!.Value, ct)).ToHttpResult(Results.Ok))
            .RequireAuthorization();
    }

    private sealed record CsrfTokenDto(string HeaderName, string Token);
}
