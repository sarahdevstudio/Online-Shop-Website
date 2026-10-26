using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shop.Domain.Content;

namespace Shop.Infrastructure.Persistence.Configurations;

internal sealed class ContentPageConfiguration : IEntityTypeConfiguration<ContentPage>
{
    public void Configure(EntityTypeBuilder<ContentPage> builder)
    {
        builder.ToTable("ContentPages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Slug).IsRequired().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(ContentPage.TitleMaxLength);
        builder.Property(x => x.Body).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Ignore(x => x.IsDraft);

        builder.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("UX_ContentPages_Slug");
    }
}

internal sealed class ContentAuditLogConfiguration : IEntityTypeConfiguration<ContentAuditLog>
{
    public void Configure(EntityTypeBuilder<ContentAuditLog> builder)
    {
        builder.ToTable("ContentAuditLogs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Slug).IsRequired().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.AtUtc).HasColumnType("datetime2");

        builder.HasIndex(x => new { x.Slug, x.AtUtc }).HasDatabaseName("IX_ContentAuditLogs_Slug_AtUtc");
    }
}
