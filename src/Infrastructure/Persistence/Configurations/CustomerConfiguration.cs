using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shop.Domain.Customers;
using Shop.Infrastructure.Identity;

namespace Shop.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FullName).IsRequired().HasMaxLength(Customer.FullNameMaxLength);
        builder.Property(x => x.Phone).HasMaxLength(Customer.PhoneMaxLength);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<Customer>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("UX_Customers_UserId");
    }
}
