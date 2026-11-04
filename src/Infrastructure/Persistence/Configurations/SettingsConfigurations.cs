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

internal sealed class StaffAuditLogConfiguration : IEntityTypeConfiguration<Shop.Domain.Security.StaffAuditLog>
{
    public void Configure(EntityTypeBuilder<Shop.Domain.Security.StaffAuditLog> builder)
    {
        builder.ToTable("StaffAuditLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AtUtc).HasColumnType("datetime2");
        builder.Property(x => x.Action).IsRequired().HasMaxLength(Shop.Domain.Security.StaffAuditLog.ActionMaxLength).IsUnicode(false);
        builder.Property(x => x.Subject).HasMaxLength(Shop.Domain.Security.StaffAuditLog.NameMaxLength);
        builder.Property(x => x.Detail).HasMaxLength(Shop.Domain.Security.StaffAuditLog.DetailMaxLength);
        builder.HasIndex(x => x.AtUtc).HasDatabaseName("IX_StaffAuditLogs_AtUtc");
    }
}

internal sealed class CatalogTransferLogConfiguration : IEntityTypeConfiguration<Shop.Domain.Catalog.CatalogTransferLog>
{
    public void Configure(EntityTypeBuilder<Shop.Domain.Catalog.CatalogTransferLog> builder)
    {
        builder.ToTable("CatalogTransferLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AtUtc).HasColumnType("datetime2");
        builder.Property(x => x.Kind).IsRequired().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.FileName).IsRequired().HasMaxLength(Shop.Domain.Catalog.CatalogTransferLog.FileNameMaxLength);
        builder.HasIndex(x => x.AtUtc).HasDatabaseName("IX_CatalogTransferLogs_AtUtc");
    }
}

internal sealed class SeoMetaConfiguration : IEntityTypeConfiguration<Shop.Domain.Seo.SeoMeta>
{
    public void Configure(EntityTypeBuilder<Shop.Domain.Seo.SeoMeta> builder)
    {
        builder.ToTable("SeoMetas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TargetType).IsRequired().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.MetaTitle).HasMaxLength(Shop.Domain.Seo.SeoMeta.TitleMaxLength);
        builder.Property(x => x.MetaDescription).HasMaxLength(Shop.Domain.Seo.SeoMeta.DescriptionMaxLength);
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.TargetType, x.TargetId }).IsUnique().HasDatabaseName("UX_SeoMetas_Target");
        builder.HasIndex(x => x.NoIndex).HasFilter("[NoIndex] = 1").HasDatabaseName("IX_SeoMetas_NoIndex");
    }
}

internal sealed class FaqItemConfiguration : IEntityTypeConfiguration<Shop.Domain.Seo.FaqItem>
{
    public void Configure(EntityTypeBuilder<Shop.Domain.Seo.FaqItem> builder)
    {
        builder.ToTable("FaqItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TargetType).IsRequired().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Question).IsRequired().HasMaxLength(Shop.Domain.Seo.FaqItem.QuestionMaxLength);
        builder.Property(x => x.Answer).IsRequired().HasMaxLength(Shop.Domain.Seo.FaqItem.AnswerMaxLength);
        builder.HasIndex(x => new { x.TargetType, x.TargetId, x.SortOrder }).HasDatabaseName("IX_FaqItems_Target");
    }
}
