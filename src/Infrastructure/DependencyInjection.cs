using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shop.Application.Accounts;
using Shop.Application.Admin;
using Shop.Application.Catalog;
using Shop.Application.Content;
using Shop.Infrastructure.Content;
using Shop.Application.Orders;
using Shop.Application.Payments;
using Shop.Application.Settings;
using Shop.Infrastructure.Settings;
using Shop.Infrastructure.Orders;
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
                options.User.RequireUniqueEmail = false; // admins sign in by username and may have no e-mail; customers' username IS their e-mail (unique)
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
            .AddErrorDescriber<PersianIdentityErrorDescriber>()
            .AddEntityFrameworkStores<ShopDbContext>()
            .AddSignInManager();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<LoginThrottle>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<ICatalogAdminService, CatalogAdminService>();
        services.AddScoped<IStockService, StockService>();
        services.AddScoped<IStockAdminService, StockAdminService>();
        services.AddScoped<IContentService, ContentService>();
        services.AddScoped<IAdminContentService, AdminContentService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.Configure<CheckoutOptions>(configuration.GetSection(CheckoutOptions.SectionName));
        services.Configure<StoreDefaultsOptions>(configuration.GetSection(StoreDefaultsOptions.SectionName));
        services.AddSingleton<StoreSettingsProvider>();
        services.AddSingleton<IStoreSettings>(sp => sp.GetRequiredService<StoreSettingsProvider>());
        services.AddScoped<IStoreInfoService, StoreInfoService>();
        services.AddScoped<IAdminSettingsService, AdminSettingsService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IAdminOrderService, AdminOrderService>();
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
