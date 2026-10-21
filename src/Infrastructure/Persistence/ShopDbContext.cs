using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Shop.Domain.Customers;
using Shop.Infrastructure.Identity;

namespace Shop.Infrastructure.Persistence;

public sealed class ShopDbContext(DbContextOptions<ShopDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

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

        builder.ApplyConfigurationsFromAssembly(typeof(ShopDbContext).Assembly);
    }
}
