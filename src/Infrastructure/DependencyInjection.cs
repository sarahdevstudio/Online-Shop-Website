using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shop.Application.Accounts;
using Shop.Application.Catalog;
using Shop.Application.Payments;
using Shop.Infrastructure.Accounts;
using Shop.Infrastructure.Catalog;
using Shop.Infrastructure.Identity;
using Shop.Infrastructure.Payments;
using Shop.Infrastructure.Persistence;

namespace Shop.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Default is not configured. Set the ConnectionStrings__Default environment variable or user-secrets.");
        }

        services.AddDbContext<ShopDbContext>(options => options.UseSqlServer(connectionString));

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = PasswordRules.MinLength;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<ShopDbContext>()
            .AddSignInManager();

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<ICatalogAdminService, CatalogAdminService>();
        services.AddScoped<IStockService, StockService>();
        services.Configure<MediaOptions>(configuration.GetSection(MediaOptions.SectionName));
        services.AddSingleton<IMediaStorage, LocalMediaStorage>();

        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));
        services.AddScoped<CatalogSeeder>();
        services.AddScoped<DatabaseInitializer>();

        if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        {
            services.AddSingleton<FakePaymentGateway>();
            services.AddSingleton<IPaymentGateway>(sp => sp.GetRequiredService<FakePaymentGateway>());
        }

        return services;
    }
}
