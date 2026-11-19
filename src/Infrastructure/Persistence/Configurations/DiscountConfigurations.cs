using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shop.Domain.Discounts;

namespace Shop.Infrastructure.Persistence.Configurations;

internal sealed class DiscountCodeConfiguration : IEntityTypeConfiguration<DiscountCode>
{
    public void Configure(EntityTypeBuilder<DiscountCode> builder)
    {
        builder.ToTable("DiscountCodes", t =>
        {
            t.HasCheckConstraint("CK_DiscountCodes_Type", "[Type] IN (0, 1)");
            t.HasCheckConstraint("CK_DiscountCodes_Value", "[Value] > 0");
            t.HasCheckConstraint("CK_DiscountCodes_MinSubtotal", "[MinSubtotal] IS NULL OR [MinSubtotal] >= 0");
            t.HasCheckConstraint("CK_DiscountCodes_UsageLimit", "[UsageLimit] IS NULL OR [UsageLimit] >= 1");
            t.HasCheckConstraint("CK_DiscountCodes_PerMobileLimit", "[PerMobileLimit] >= 1");
        });
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(DiscountCode.CodeMaxLength).IsUnicode(false);
        builder.Property(x => x.ExpiresAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_DiscountCodes_Code");
    }
}

internal sealed class DiscountAuditLogConfiguration : IEntityTypeConfiguration<DiscountAuditLog>
{
    public void Configure(EntityTypeBuilder<DiscountAuditLog> builder)
    {
        builder.ToTable("DiscountAuditLogs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(DiscountCode.CodeMaxLength).IsUnicode(false);
        builder.Property(x => x.Action).IsRequired().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.AtUtc).HasColumnType("datetime2");

        builder.HasIndex(x => new { x.Code, x.AtUtc }).HasDatabaseName("IX_DiscountAuditLogs_Code_AtUtc");
    }
}
