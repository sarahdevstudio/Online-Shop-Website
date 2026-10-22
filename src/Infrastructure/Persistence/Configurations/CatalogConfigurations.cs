using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shop.Domain.Catalog;

namespace Shop.Infrastructure.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(Category.NameMaxLength);
        builder.Property(x => x.Slug).IsRequired().HasMaxLength(Slug.MaxLength).IsUnicode(false);
        builder.Property(x => x.Description).HasMaxLength(Category.DescriptionMaxLength);

        builder.HasOne<Category>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("UX_Categories_Slug");
        builder.HasIndex(x => x.ParentId).HasDatabaseName("IX_Categories_ParentId");
        builder.ToTable(t => t.HasCheckConstraint("CK_Categories_NotOwnParent", "[ParentId] IS NULL OR [ParentId] <> [Id]"));
    }
}

internal sealed class BrandConfiguration : IEntityTypeConfiguration<Brand>
{
    public void Configure(EntityTypeBuilder<Brand> builder)
    {
        builder.ToTable("Brands");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(Brand.NameMaxLength);
        builder.Property(x => x.Slug).IsRequired().HasMaxLength(Slug.MaxLength).IsUnicode(false);

        builder.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("UX_Brands_Slug");
        builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("UX_Brands_Name");
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(Product.NameMaxLength);
        builder.Property(x => x.Slug).IsRequired().HasMaxLength(Slug.MaxLength).IsUnicode(false);
        builder.Property(x => x.SearchKey).IsRequired().HasMaxLength(Product.SearchKeyMaxLength);
        builder.Property(x => x.Description).HasMaxLength(Product.DescriptionMaxLength);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");

        builder.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Brand>().WithMany().HasForeignKey(x => x.BrandId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Variants).WithOne().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Images).WithOne().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.AttributeValues).WithOne().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("UX_Products_Slug");
        builder.HasIndex(x => new { x.CategoryId, x.Status }).HasDatabaseName("IX_Products_CategoryId_Status");
        builder.HasIndex(x => x.BrandId).HasDatabaseName("IX_Products_BrandId");
        builder.ToTable(t => t.HasCheckConstraint("CK_Products_Status", "[Status] IN (0, 1, 2)"));
    }
}

internal sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("ProductVariants", t =>
        {
            t.HasCheckConstraint("CK_ProductVariants_Price", "[Price] > 0");
            t.HasCheckConstraint("CK_ProductVariants_CompareAtPrice", "[CompareAtPrice] IS NULL OR [CompareAtPrice] > [Price]");
            t.HasCheckConstraint("CK_ProductVariants_Stock", "[Stock] >= 0");
        });
        builder.HasKey(x => x.Id);
        // Lets attribute values reference (ProductId, VariantId), so a value can't point at another product's variant.
        builder.HasAlternateKey(x => new { x.ProductId, x.Id }).HasName("AK_ProductVariants_ProductId_Id");

        builder.Property(x => x.Sku).IsRequired().HasMaxLength(ProductVariant.SkuMaxLength).IsUnicode(false);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.Sku).IsUnique().HasDatabaseName("UX_ProductVariants_Sku");
    }
}

internal sealed class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("ProductImages");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Path).IsRequired().HasMaxLength(ProductImage.PathMaxLength);
        builder.Property(x => x.Alt).IsRequired().HasMaxLength(ProductImage.AltMaxLength);

        builder.HasIndex(x => new { x.ProductId, x.SortOrder }).HasDatabaseName("IX_ProductImages_ProductId_SortOrder");
    }
}

internal sealed class AttributeDefinitionConfiguration : IEntityTypeConfiguration<AttributeDefinition>
{
    public void Configure(EntityTypeBuilder<AttributeDefinition> builder)
    {
        builder.ToTable("Attributes", t => t.HasCheckConstraint("CK_Attributes_Type", "[Type] IN (0, 1)"));
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).IsRequired().HasMaxLength(AttributeDefinition.CodeMaxLength).IsUnicode(false);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(AttributeDefinition.NameMaxLength);
        builder.Property(x => x.Unit).HasMaxLength(AttributeDefinition.UnitMaxLength);

        builder.HasMany(x => x.Options).WithOne().HasForeignKey(x => x.AttributeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_Attributes_Code");
    }
}

internal sealed class AttributeOptionConfiguration : IEntityTypeConfiguration<AttributeOption>
{
    public void Configure(EntityTypeBuilder<AttributeOption> builder)
    {
        builder.ToTable("AttributeOptions");
        builder.HasKey(x => x.Id);
        // Lets attribute values reference (AttributeId, OptionId), so an option can't be used for the wrong attribute.
        builder.HasAlternateKey(x => new { x.AttributeId, x.Id }).HasName("AK_AttributeOptions_AttributeId_Id");

        builder.Property(x => x.Value).IsRequired().HasMaxLength(AttributeOption.ValueMaxLength);
        builder.Property(x => x.Slug).IsRequired().HasMaxLength(Slug.MaxLength).IsUnicode(false);

        builder.HasIndex(x => new { x.AttributeId, x.Slug }).IsUnique().HasDatabaseName("UX_AttributeOptions_AttributeId_Slug");
    }
}

internal sealed class ProductAttributeValueConfiguration : IEntityTypeConfiguration<ProductAttributeValue>
{
    public void Configure(EntityTypeBuilder<ProductAttributeValue> builder)
    {
        builder.ToTable("ProductAttributeValues", t =>
            t.HasCheckConstraint("CK_ProductAttributeValues_OptionXorText",
                "([OptionId] IS NULL AND [Text] IS NOT NULL) OR ([OptionId] IS NOT NULL AND [Text] IS NULL)"));
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Text).HasMaxLength(ProductAttributeValue.TextMaxLength);

        builder.HasOne<AttributeDefinition>().WithMany().HasForeignKey(x => x.AttributeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AttributeOption>().WithMany()
            .HasForeignKey(x => new { x.AttributeId, x.OptionId })
            .HasPrincipalKey(x => new { x.AttributeId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        // No cascade here: the product cascade already removes values (SQL Server allows one cascade path).
        builder.HasOne(x => x.Variant).WithMany()
            .HasForeignKey(x => new { x.ProductId, x.VariantId })
            .HasPrincipalKey(x => new { x.ProductId, x.Id })
            .OnDelete(DeleteBehavior.NoAction);

        // One value per attribute per product (VariantId NULL) or per variant. HasFilter(null): SQL Server treats NULLs
        // as equal in a unique index, which is exactly "one product-level value per attribute".
        builder.HasIndex(x => new { x.ProductId, x.VariantId, x.AttributeId }).IsUnique().HasFilter(null)
            .HasDatabaseName("UX_ProductAttributeValues_ProductId_VariantId_AttributeId");
        builder.HasIndex(x => new { x.AttributeId, x.OptionId }).HasDatabaseName("IX_ProductAttributeValues_AttributeId_OptionId");
    }
}
