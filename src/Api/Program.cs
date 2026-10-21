using Serilog;
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

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseSecurityHeaders();
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

await app.RunAsync();

// Exposed so integration tests can host the API with WebApplicationFactory<Program>.
public partial class Program;
