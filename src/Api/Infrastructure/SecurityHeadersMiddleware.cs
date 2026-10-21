namespace Shop.Api.Infrastructure;

internal static class SecurityHeadersMiddleware
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["Referrer-Policy"] = "no-referrer";

                // Account and admin responses are per-user and must never be stored by shared caches.
                if (context.Request.Path.StartsWithSegments("/api/v1/auth")
                    || context.Request.Path.StartsWithSegments("/api/v1/customer")
                    || context.Request.Path.StartsWithSegments("/api/v1/admin"))
                {
                    headers.CacheControl = "no-store";
                }

                return Task.CompletedTask;
            });

            await next();
        });
}
