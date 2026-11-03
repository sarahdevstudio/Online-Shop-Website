using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shop.Domain.Site;

namespace Shop.Infrastructure.Persistence.Configurations;

internal sealed class SiteConfigVersionConfiguration : IEntityTypeConfiguration<SiteConfigVersion>
{
    public void Configure(EntityTypeBuilder<SiteConfigVersion> builder)
    {
        builder.ToTable("SiteConfigVersions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Kind).HasConversion(kind => kind.ToRoute(), value => Enum.Parse<SiteKind>(value, true))
            .IsRequired().HasMaxLength(10).IsUnicode(false);
        builder.Property(x => x.State).HasConversion(state => state.ToDb(), value => SiteNames.ParseState(value))
            .IsRequired().HasMaxLength(10).IsUnicode(false);
        builder.Property(x => x.Payload).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.PublishedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.RowVersion).IsRowVersion();

        // At most one draft and one published row per kind; a version number is used once per kind.
        builder.HasIndex(x => x.Kind, "UX_SiteConfigVersions_Kind_Draft").IsUnique().HasFilter("[State] = 'draft'");
        builder.HasIndex(x => x.Kind, "UX_SiteConfigVersions_Kind_Published").IsUnique().HasFilter("[State] = 'published'");
        builder.HasIndex(x => new { x.Kind, x.Version }, "UX_SiteConfigVersions_Kind_Version").IsUnique().HasFilter("[Version] IS NOT NULL");
    }
}

internal sealed class SiteMediaConfiguration : IEntityTypeConfiguration<SiteMedia>
{
    public void Configure(EntityTypeBuilder<SiteMedia> builder)
    {
        builder.ToTable("SiteMedia");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Path).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ContentType).IsRequired().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Purpose).IsRequired().HasMaxLength(12).IsUnicode(false);
        builder.Property(x => x.UploadedAtUtc).HasColumnType("datetime2");
    }
}

internal sealed class SiteAuditLogConfiguration : IEntityTypeConfiguration<SiteAuditLog>
{
    public void Configure(EntityTypeBuilder<SiteAuditLog> builder)
    {
        builder.ToTable("SiteAuditLogs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Kind).HasConversion(kind => kind.ToRoute(), value => Enum.Parse<SiteKind>(value, true))
            .IsRequired().HasMaxLength(10).IsUnicode(false);
        builder.Property(x => x.Action).HasConversion(action => action.ToDb(), value => SiteNames.ParseAction(value))
            .IsRequired().HasMaxLength(12).IsUnicode(false);
        builder.Property(x => x.Summary).IsRequired().HasMaxLength(SiteAuditLog.SummaryMaxLength);
        builder.Property(x => x.AtUtc).HasColumnType("datetime2");

        builder.HasIndex(x => x.AtUtc).HasDatabaseName("IX_SiteAuditLogs_AtUtc");
        builder.HasIndex(x => new { x.Kind, x.AtUtc }).HasDatabaseName("IX_SiteAuditLogs_Kind_AtUtc");
    }
}
