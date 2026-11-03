using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shop.Domain.Translations;

namespace Shop.Infrastructure.Persistence.Configurations;

internal sealed class TranslationConfiguration : IEntityTypeConfiguration<Translation>
{
    public void Configure(EntityTypeBuilder<Translation> builder)
    {
        builder.ToTable("Translations");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).IsRequired().HasMaxLength(Translation.KeyMaxLength).IsUnicode(false);
        builder.Property(x => x.Culture).IsRequired().HasMaxLength(Translation.CultureMaxLength).IsUnicode(false).HasDefaultValue(Translation.DefaultCulture);
        builder.Property(x => x.Text).IsRequired().HasMaxLength(Translation.TextMaxLength);
        builder.Property(x => x.DefaultText).IsRequired().HasMaxLength(Translation.TextMaxLength);
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Ignore(x => x.IsEdited);

        builder.HasIndex(x => new { x.Title, x.Culture }).IsUnique().HasDatabaseName("UX_Translations_Title_Culture");
    }
}

internal sealed class TranslationAuditLogConfiguration : IEntityTypeConfiguration<TranslationAuditLog>
{
    public void Configure(EntityTypeBuilder<TranslationAuditLog> builder)
    {
        builder.ToTable("TranslationAuditLogs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).IsRequired().HasMaxLength(Translation.KeyMaxLength).IsUnicode(false);
        builder.Property(x => x.Culture).IsRequired().HasMaxLength(Translation.CultureMaxLength).IsUnicode(false);
        builder.Property(x => x.OldText).IsRequired().HasMaxLength(Translation.TextMaxLength);
        builder.Property(x => x.NewText).IsRequired().HasMaxLength(Translation.TextMaxLength);
        builder.Property(x => x.AtUtc).HasColumnType("datetime2");

        builder.HasIndex(x => new { x.Title, x.Culture, x.AtUtc }).HasDatabaseName("IX_TranslationAuditLogs_Title_AtUtc");
    }
}
