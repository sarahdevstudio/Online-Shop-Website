using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shop.Domain.Blog;
using Shop.Domain.Catalog;

namespace Shop.Infrastructure.Persistence.Configurations;

internal sealed class BlogPostConfiguration : IEntityTypeConfiguration<BlogPost>
{
    public void Configure(EntityTypeBuilder<BlogPost> builder)
    {
        builder.ToTable("BlogPosts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Slug).IsRequired().HasMaxLength(Slug.MaxLength).IsUnicode(false);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(BlogPost.TitleMaxLength);
        builder.Property(x => x.Excerpt).IsRequired().HasMaxLength(BlogPost.ExcerptMaxLength);
        builder.Property(x => x.Body).IsRequired();
        builder.Property(x => x.PublishedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("UX_BlogPosts_Slug");
        builder.HasIndex(x => new { x.IsPublished, x.PublishedAtUtc }).HasDatabaseName("IX_BlogPosts_IsPublished_PublishedAtUtc");
    }
}

internal sealed class BlogAuditLogConfiguration : IEntityTypeConfiguration<BlogAuditLog>
{
    public void Configure(EntityTypeBuilder<BlogAuditLog> builder)
    {
        builder.ToTable("BlogAuditLogs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Slug).IsRequired().HasMaxLength(Slug.MaxLength).IsUnicode(false);
        builder.Property(x => x.Action).IsRequired().HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.AtUtc).HasColumnType("datetime2");

        builder.HasIndex(x => new { x.Slug, x.AtUtc }).HasDatabaseName("IX_BlogAuditLogs_Slug_AtUtc");
    }
}
