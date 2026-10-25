using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Shop.Application.Orders;
using Shop.Application.Accounts;
using Shop.Application.Common;
using Shop.Domain.Security;
using Shop.Infrastructure.Persistence;

namespace Shop.Api.Infrastructure;

/// <summary>Settings that must be real before a production start.</summary>
public static class StartupChecks
{
    /// <summary>
    /// Throws when the storefront key is missing or a placeholder. Card details are not checked here: checkout simply does not
    /// offer card-to-card until the store settings hold a valid card.
    /// </summary>
    public static void RequireProductionSettings(IConfiguration configuration)
    {
        if (!StorefrontKeyRules.IsAcceptableForProduction(configuration[$"{StorefrontOptions.SectionName}:{nameof(StorefrontOptions.ApiKey)}"]))
        {
            throw new InvalidOperationException(
                $"Storefront:ApiKey must be set to a random secret of at least {StorefrontKeyRules.MinLength} characters (not a placeholder).");
        }

    }
}

internal static class StoreEndpointsPolicies
{
    public const string Cart = Endpoints.StoreEndpoints.CartPolicy;
    public const string Order = Endpoints.StoreEndpoints.OrderPolicy;
    public const string Lookup = Endpoints.StoreEndpoints.LookupPolicy;
}

public static class ApiServiceCollectionExtensions
{
    public const string AuthRateLimitPolicy = "auth";
    public const string CustomerPolicy = "Customer";

    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        if (environment.IsProduction())
        {
            StartupChecks.RequireProductionSettings(configuration);
        }


        services.AddHttpContextAccessor();
        services.Configure<StorefrontOptions>(configuration.GetSection(StorefrontOptions.SectionName));
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            // Only X-Forwarded-For, one hop, and only from the configured storefront address(es); from anyone else the
            // header is ignored, so a browser-supplied value can never choose its own rate-limit key.
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
            options.ForwardLimit = 1;
            var trusted = configuration.GetSection($"{StorefrontOptions.SectionName}:{nameof(StorefrontOptions.TrustedProxies)}").Get<string[]>() ?? [];
            if (trusted.Length > 0)
            {
                options.KnownProxies.Clear();
                options.KnownIPNetworks.Clear();
                foreach (var entry in trusted)
                {
                    if (entry.Contains('/'))
                    {
                        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(entry));
                    }
                    else
                    {
                        options.KnownProxies.Add(IPAddress.Parse(entry));
                    }
                }
            }
        });
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

            // Store limits are per client IP (the real one, via the trusted storefront); read lazily from options.
            options.AddPolicy(StoreEndpointsPolicies.Cart, http => ForIp(http, 120, TimeSpan.FromMinutes(1)));
            options.AddPolicy(StoreEndpointsPolicies.Lookup, http => ForIp(http, 30, TimeSpan.FromMinutes(1)));
            options.AddPolicy(StoreEndpointsPolicies.Order, http =>
            {
                var checkout = http.RequestServices.GetRequiredService<IOptions<CheckoutOptions>>().Value;
                return ForIp(http, checkout.OrderPermitLimit, TimeSpan.FromMinutes(checkout.OrderWindowMinutes));
            });
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

    private static RateLimitPartition<string> ForIp(HttpContext http, int permits, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(
            $"{http.GetEndpoint()?.DisplayName}|{ClientIp.From(http)}",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = window, QueueLimit = 0 });

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
