using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Shop.Domain.Catalog;
using Shop.Domain.Orders;

namespace Shop.Infrastructure.Persistence.Configurations;

internal sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("Carts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TokenHash).IsRequired().HasMaxLength(32).IsFixedLength();
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.LastActivityAtUtc).HasColumnType("datetime2");

        builder.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.CartId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("UX_Carts_TokenHash");
        builder.HasIndex(x => x.LastActivityAtUtc).HasDatabaseName("IX_Carts_LastActivityAtUtc");
    }
}

internal sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("CartItems", t =>
        {
            t.HasCheckConstraint("CK_CartItems_Quantity", "[Quantity] > 0");
            t.HasCheckConstraint("CK_CartItems_SeenPrice", "[SeenPrice] >= 0");
        });
        builder.HasKey(x => x.Id);

        builder.HasOne<ProductVariant>().WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.CartId, x.VariantId }).IsUnique().HasDatabaseName("UX_CartItems_CartId_VariantId");
        builder.HasIndex(x => x.VariantId).HasDatabaseName("IX_CartItems_VariantId");
    }
}

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", t =>
        {
            t.HasCheckConstraint("CK_Orders_Status", "[Status] IN (0, 1, 2, 3, 4, 5)");
            t.HasCheckConstraint("CK_Orders_PaymentStatus", "[PaymentStatus] IN (0, 1)");
            t.HasCheckConstraint("CK_Orders_PaymentMethod", "[PaymentMethod] IN (0, 1)");
            t.HasCheckConstraint("CK_Orders_Amounts", "[Subtotal] >= 0 AND [ShippingFee] >= 0");
            t.HasCheckConstraint("CK_Orders_Total", "[Total] = [Subtotal] + [ShippingFee]");
        });
        builder.HasKey(x => x.Id);

        // Human-readable sequential number from a database sequence (gaps are possible when a transaction rolls back).
        builder.Property(x => x.Number).HasDefaultValueSql("NEXT VALUE FOR [OrderNumbers]").ValueGeneratedOnAdd();
        builder.Property(x => x.AccessToken).IsRequired().HasMaxLength(Order.AccessTokenLength).IsUnicode(false).IsFixedLength();
        builder.Property(x => x.FullName).IsRequired().HasMaxLength(Order.NameMaxLength);
        builder.Property(x => x.Mobile).IsRequired().HasMaxLength(11).IsUnicode(false).IsFixedLength();
        builder.Property(x => x.Province).IsRequired().HasMaxLength(Order.ProvinceMaxLength);
        builder.Property(x => x.City).IsRequired().HasMaxLength(Order.CityMaxLength);
        builder.Property(x => x.Address).IsRequired().HasMaxLength(Order.AddressMaxLength);
        builder.Property(x => x.PostalCode).IsRequired().HasMaxLength(10).IsUnicode(false).IsFixedLength();
        builder.Property(x => x.Note).HasMaxLength(Order.NoteMaxLength);
        builder.Property(x => x.ClientIp).IsRequired().HasMaxLength(Order.ClientIpMaxLength).IsUnicode(false);
        builder.Property(x => x.PaymentCardNumber).HasMaxLength(16).IsUnicode(false);
        builder.Property(x => x.PaymentCardHolder).HasMaxLength(Shop.Domain.Settings.StoreSettingsRules.HolderMaxLength);
        builder.Property(x => x.PaymentBankName).HasMaxLength(Shop.Domain.Settings.StoreSettingsRules.BankMaxLength);
        builder.Property(x => x.TransferTracking).HasMaxLength(Order.TrackingMaxLength);
        builder.Property(x => x.TransferTime).HasMaxLength(Order.TransferTimeMaxLength);
        builder.Property(x => x.TransferCardLast4).HasMaxLength(4).IsUnicode(false).IsFixedLength();
        builder.Property(x => x.CreatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("datetime2");
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.StatusLog).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.Number).IsUnique().HasDatabaseName("UX_Orders_Number");
        builder.HasIndex(x => x.AccessToken).IsUnique().HasDatabaseName("UX_Orders_AccessToken");
        builder.HasIndex(x => x.IdempotencyKey).IsUnique().HasDatabaseName("UX_Orders_IdempotencyKey");
        builder.HasIndex(x => new { x.Status, x.CreatedAtUtc }).HasDatabaseName("IX_Orders_Status_CreatedAtUtc");
        builder.HasIndex(x => new { x.Mobile, x.Status }).HasDatabaseName("IX_Orders_Mobile_Status");
        builder.HasIndex(x => new { x.ClientIp, x.Status }).HasDatabaseName("IX_Orders_ClientIp_Status");
    }
}

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems", t =>
        {
            t.HasCheckConstraint("CK_OrderItems_Quantity", "[Quantity] > 0");
            t.HasCheckConstraint("CK_OrderItems_UnitPrice", "[UnitPrice] > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Ignore(x => x.LineTotal);

        builder.Property(x => x.ProductName).IsRequired().HasMaxLength(OrderItem.ProductNameMaxLength);
        builder.Property(x => x.VariantLabel).HasMaxLength(OrderItem.VariantLabelMaxLength);
        builder.Property(x => x.Sku).IsRequired().HasMaxLength(ProductVariant.SkuMaxLength).IsUnicode(false);

        // Ordered variants can only be deactivated, never deleted.
        builder.HasOne<ProductVariant>().WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.OrderId).HasDatabaseName("IX_OrderItems_OrderId");
        builder.HasIndex(x => x.VariantId).HasDatabaseName("IX_OrderItems_VariantId");
    }
}

internal sealed class OrderStatusLogConfiguration : IEntityTypeConfiguration<OrderStatusLog>
{
    public void Configure(EntityTypeBuilder<OrderStatusLog> builder)
    {
        builder.ToTable("OrderStatusLogs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AtUtc).HasColumnType("datetime2");

        builder.HasIndex(x => x.OrderId).HasDatabaseName("IX_OrderStatusLogs_OrderId");
    }
}
