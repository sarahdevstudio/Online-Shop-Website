namespace Shop.Api.Infrastructure;

/// <summary>
/// Which deployment this is, for search-engine rules: only <c>production</c> may be indexed. <c>SITE_ENV</c>
/// (production | staging | development) decides; when it is not set, ASP.NET's environment name does (Production = production).
/// Anything else, including a typo, counts as NOT production, so the safe default is "do not index".
/// </summary>
public static class SiteEnvironment
{
    public static string Resolve(IConfiguration configuration, IHostEnvironment environment)
    {
        var value = configuration["SITE_ENV"]?.Trim().ToLowerInvariant();
        return value switch
        {
            "production" or "staging" or "development" => value,
            null or "" => environment.IsProduction() ? "production" : "development",
            _ => "development",
        };
    }

    public static bool IsIndexable(IConfiguration configuration, IHostEnvironment environment) =>
        Resolve(configuration, environment) == "production";
}

internal static class NoIndexMiddleware
{
    public const string RobotsTxt = "User-agent: *\nDisallow: /\n";

    /// <summary>Every response of a non-production deployment says "noindex, nofollow" (read per request, so it follows config).</summary>
    public static IApplicationBuilder UseNoIndexOutsideProduction(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var services = context.RequestServices;
            if (!SiteEnvironment.IsIndexable(services.GetRequiredService<IConfiguration>(), services.GetRequiredService<IHostEnvironment>()))
            {
                context.Response.OnStarting(() =>
                {
                    context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
                    return Task.CompletedTask;
                });
            }

            await next();
        });

    /// <summary>The API is not a website: its robots.txt always disallows everything, in every environment.</summary>
    public static void MapRobotsTxt(this IEndpointRouteBuilder app) =>
        app.MapGet("/robots.txt", () => Results.Text(RobotsTxt, "text/plain")).AllowAnonymous();
}
