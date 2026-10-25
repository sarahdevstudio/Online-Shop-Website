using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Shop.Domain.Catalog;
using Shop.Domain.Customers;
using Shop.Domain.Inventory;
using Shop.Domain.Orders;
using Shop.Domain.Settings;
using Shop.Infrastructure.Identity;

namespace Shop.Infrastructure.Persistence;

public sealed class ShopDbContext(DbContextOptions<ShopDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<AttributeDefinition> Attributes => Set<AttributeDefinition>();
    public DbSet<AttributeOption> AttributeOptions => Set<AttributeOption>();
    public DbSet<ProductAttributeValue> ProductAttributeValues => Set<ProductAttributeValue>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusLog> OrderStatusLogs => Set<OrderStatusLog>();
    public DbSet<StoreSetting> StoreSettings => Set<StoreSetting>();
    public DbSet<SettingsAuditLog> SettingsAuditLogs => Set<SettingsAuditLog>();
    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(user =>
        {
            user.ToTable("Users");
            user.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");

            // Identity's default email index is non-unique; the UserName index already prevents
            // duplicates, this makes the email rule explicit and enforced by the database.
            user.HasIndex(x => x.NormalizedEmail)
                .HasDatabaseName("UX_Users_NormalizedEmail")
                .IsUnique()
                .HasFilter("[NormalizedEmail] IS NOT NULL");
        });

        builder.Entity<ApplicationRole>().ToTable("Roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");

        builder.HasSequence<int>("OrderNumbers").StartsAt(1001).IncrementsBy(1);

        builder.ApplyConfigurationsFromAssembly(typeof(ShopDbContext).Assembly);
    }
}
