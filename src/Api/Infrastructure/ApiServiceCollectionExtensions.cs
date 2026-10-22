using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Shop.Application.Accounts;
using Shop.Application.Common;
using Shop.Domain.Security;
using Shop.Infrastructure.Persistence;

namespace Shop.Api.Infrastructure;

public static class ApiServiceCollectionExtensions
{
    public const string AuthRateLimitPolicy = "auth";
    public const string CustomerPolicy = "Customer";

    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        services.AddValidatorsFromAssemblyContaining<RegisterCustomerRequestValidator>();

        // Enums travel as camelCase strings ("published"), never as numbers.
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddOpenApi();
        services.AddHealthChecks().AddDbContextCheck<ShopDbContext>("database", tags: ["ready"]);

        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        var requireSecureCookie = configuration.GetValue("Auth:RequireSecureCookie", true);
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "shop_auth";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = requireSecureCookie ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromDays(7);
            options.SlidingExpiration = true;

            // This is an API: answer 401/403 instead of redirecting to a login page.
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        services.AddAuthorizationBuilder()
            .AddPolicy(CustomerPolicy, policy => policy.RequireRole(Roles.Customer))
            .AddPermissionPolicies();

        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-XSRF-TOKEN";
            options.Cookie.Name = "shop_csrf";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = requireSecureCookie ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
        });

        var permitLimit = configuration.GetValue("RateLimiting:AuthPermitLimit", 10);
        var windowSeconds = configuration.GetValue("RateLimiting:WindowSeconds", 60);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AuthRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromSeconds(windowSeconds),
                        QueueLimit = 0,
                    }));
        });

        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options =>
        {
            if (origins.Length > 0)
            {
                options.AddDefaultPolicy(policy => policy
                    .WithOrigins(origins)
                    .WithHeaders("Content-Type", "X-XSRF-TOKEN")
                    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
                    .AllowCredentials());
            }
        });

        return services;
    }

    /// <summary>One policy per permission, named exactly like the permission ("Settings.Manage").</summary>
    private static AuthorizationBuilder AddPermissionPolicies(this AuthorizationBuilder builder)
    {
        foreach (var permission in Permissions.All)
        {
            builder.AddPolicy(permission, policy => policy.RequireClaim(Permissions.ClaimType, permission));
        }

        return builder;
    }
}
