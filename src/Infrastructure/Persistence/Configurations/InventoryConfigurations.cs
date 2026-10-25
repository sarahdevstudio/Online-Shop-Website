using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shop.Domain.Catalog;
using Shop.Domain.Inventory;

namespace Shop.Infrastructure.Persistence.Configurations;

internal sealed class StockAdjustmentConfiguration : IEntityTypeConfiguration<StockAdjustment>
{
    public void Configure(EntityTypeBuilder<StockAdjustment> builder)
    {
        builder.ToTable("StockAdjustments", t => t.HasCheckConstraint("CK_StockAdjustments_StockAfter", "[StockAfter] >= 0"));
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Reason).HasMaxLength(StockAdjustment.ReasonMaxLength);
        builder.Property(x => x.Source).IsRequired().HasMaxLength(10).IsUnicode(false);
        builder.Property(x => x.AtUtc).HasColumnType("datetime2");

        builder.HasOne<ProductVariant>().WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.VariantId, x.AtUtc }).HasDatabaseName("IX_StockAdjustments_VariantId_AtUtc");
    }
}
