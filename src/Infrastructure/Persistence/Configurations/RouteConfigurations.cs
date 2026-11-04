using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shop.Domain.Routing;

namespace Shop.Infrastructure.Persistence.Configurations;

internal sealed class RouteEntryConfiguration : IEntityTypeConfiguration<RouteEntry>
{
    /// <summary>Case-insensitive and accent-sensitive; paths are lower-case ASCII anyway, this keeps the unique index honest.</summary>
    public const string PathCollation = "Latin1_General_100_CI_AS";

    public void Configure(EntityTypeBuilder<RouteEntry> builder)
    {
        builder.ToTable("RoutePaths");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Path).IsRequired().HasMaxLength(RoutePath.MaxLength).IsUnicode(false).UseCollation(PathCollation);
        builder.Property(x => x.TargetType).IsRequired().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.RowVersion).IsRowVersion();

        // A path belongs to one row, current or old: two entities can never share (or race for) the same address.
        builder.HasIndex(x => x.Path).IsUnique().HasDatabaseName("UX_RoutePaths_Path");

        // Exactly one current path per entity (filtered unique index).
        builder.HasIndex(x => new { x.TargetType, x.TargetId }).IsUnique().HasFilter("[IsCurrent] = 1").HasDatabaseName("UX_RoutePaths_Current");
    }
}

internal sealed class RouteAuditLogConfiguration : IEntityTypeConfiguration<RouteAuditLog>
{
    public void Configure(EntityTypeBuilder<RouteAuditLog> builder)
    {
        builder.ToTable("RouteAuditLogs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TargetType).IsRequired().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Action).IsRequired().HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.OldPath).HasMaxLength(RoutePath.MaxLength).IsUnicode(false);
        builder.Property(x => x.NewPath).HasMaxLength(RoutePath.MaxLength).IsUnicode(false);
        builder.Property(x => x.AtUtc).HasColumnType("datetime2");

        builder.HasIndex(x => x.AtUtc).HasDatabaseName("IX_RouteAuditLogs_AtUtc");
        builder.HasIndex(x => new { x.TargetType, x.TargetId, x.AtUtc }).HasDatabaseName("IX_RouteAuditLogs_Target");
    }
}
