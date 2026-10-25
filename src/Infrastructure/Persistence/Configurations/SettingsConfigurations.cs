using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shop.Domain.Settings;

namespace Shop.Infrastructure.Persistence.Configurations;

internal sealed class StoreSettingConfiguration : IEntityTypeConfiguration<StoreSetting>
{
    public void Configure(EntityTypeBuilder<StoreSetting> builder)
    {
        builder.ToTable("StoreSettings", t =>
        {
            t.HasCheckConstraint("CK_StoreSettings_Singleton", "[Id] = 1");
            t.HasCheckConstraint("CK_StoreSettings_Amounts", "[ShippingFlatFee] >= 0 AND ([FreeShippingThreshold] IS NULL OR [FreeShippingThreshold] >= 0)");
            t.HasCheckConstraint("CK_StoreSettings_MaxQuantity", "[MaxQuantityPerLine] BETWEEN 1 AND 100");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.CardNumber).IsRequired().HasMaxLength(16).IsUnicode(false);
        builder.Property(x => x.CardHolderName).IsRequired().HasMaxLength(StoreSettingsRules.HolderMaxLength);
        builder.Property(x => x.BankName).IsRequired().HasMaxLength(StoreSettingsRules.BankMaxLength);
        builder.Property(x => x.StorePhone).IsRequired().HasMaxLength(StoreSettingsRules.PhoneMaxLength);
        builder.Property(x => x.StoreAddress).IsRequired().HasMaxLength(StoreSettingsRules.AddressMaxLength);
        builder.Property(x => x.WorkingHours).IsRequired().HasMaxLength(StoreSettingsRules.HoursMaxLength);
        builder.Property(x => x.InstagramHandle).IsRequired().HasMaxLength(StoreSettingsRules.InstagramMaxLength).IsUnicode(false);
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Ignore(x => x.Values);
    }
}

internal sealed class SettingsAuditLogConfiguration : IEntityTypeConfiguration<SettingsAuditLog>
{
    public void Configure(EntityTypeBuilder<SettingsAuditLog> builder)
    {
        builder.ToTable("SettingsAuditLogs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AtUtc).HasColumnType("datetime2");
        builder.Property(x => x.Field).IsRequired().HasMaxLength(SettingsAuditLog.FieldMaxLength).IsUnicode(false);
        builder.Property(x => x.OldValue).HasMaxLength(SettingsAuditLog.ValueMaxLength);
        builder.Property(x => x.NewValue).HasMaxLength(SettingsAuditLog.ValueMaxLength);

        builder.HasIndex(x => x.AtUtc).HasDatabaseName("IX_SettingsAuditLogs_AtUtc");
    }
}
