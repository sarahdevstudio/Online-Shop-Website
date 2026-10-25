using Serilog;
using Serilog.Events;
using Shop.Api.Endpoints;
using Shop.Api.Infrastructure;
using Shop.Infrastructure;
using Shop.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddApiServices(builder.Configuration, builder.Environment);

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(app.Lifetime.ApplicationStopping);
}

// First: replaces the connection's address with X-Forwarded-For, but only when the peer is a trusted proxy (the storefront).
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging(options =>
{
    // This host's own logger (not the process-wide static one), so request logs go where this host's sinks are.
    options.Logger = app.Services.GetRequiredService<Serilog.ILogger>();

    // Order access tokens are in the path; they must never reach the logs.
    options.GetMessageTemplateProperties = (httpContext, requestPath, elapsedMs, statusCode) =>
    [
        new LogEventProperty("RequestMethod", new ScalarValue(httpContext.Request.Method)),
        new LogEventProperty("RequestPath", new ScalarValue(LogPaths.Mask(requestPath))),
        new LogEventProperty("StatusCode", new ScalarValue(statusCode)),
        new LogEventProperty("Elapsed", new ScalarValue(elapsedMs)),
    ];
});

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseSecurityHeaders();
app.UseMedia();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new() { Predicate = check => check.Tags.Contains("ready") });

var api = app.MapGroup("/api/v1");
api.AddEndpointFilter<AntiforgeryFilter>();
api.MapAuthEndpoints();
api.MapCustomerEndpoints();
api.MapAdminEndpoints();
api.MapCatalogEndpoints();
api.MapCatalogAdminEndpoints();
api.MapStoreEndpoints();
api.MapOrderAdminEndpoints();
api.MapSettingsAdminEndpoints();

await app.RunAsync();

// Exposed so integration tests can host the API with WebApplicationFactory<Program>.
public partial class Program;
