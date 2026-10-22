using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Shop.Application.Catalog;

namespace Shop.IntegrationTests;

/// <summary>Database-level guarantees: they must hold even for writes that bypass the services.</summary>
public sealed class CatalogPersistenceTests(ApiFixture fixture) : CatalogTestBase(fixture)
{
    private async Task<(AdminProductBody Product, AdminProductBody Other, Dictionary<string, AdminAttributeBody> Attributes)> ProductsAsync()
    {
        var admin = await LoginAdminAsync();
        var categoryId = (await CreateCategoryAsync(admin, Unique("cat"))).Id;
        return (await CreateProductAsync(admin, categoryId, Unique("p")), await CreateProductAsync(admin, categoryId, Unique("o")), await AttributesAsync(admin));
    }

    private Task<int> SqlAsync(string sql, params object[] args) =>
        Fixture.WithDbAsync(db => db.Database.ExecuteSqlRawAsync(sql, args));

    private async Task AssertRejectedAsync(string constraint, string sql, params object[] args)
    {
        var ex = await Assert.ThrowsAsync<SqlException>(() => SqlAsync(sql, args));
        Assert.Contains(constraint, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Variant_StockPriceAndCompareAtPrice_AreChecked()
    {
        var (product, _, _) = await ProductsAsync();
        var id = product.Variants[0].Id;

        await AssertRejectedAsync("CK_ProductVariants_Stock", "UPDATE ProductVariants SET Stock = -1 WHERE Id = {0}", id);
        await AssertRejectedAsync("CK_ProductVariants_Price", "UPDATE ProductVariants SET Price = 0 WHERE Id = {0}", id);
        await AssertRejectedAsync("CK_ProductVariants_CompareAtPrice", "UPDATE ProductVariants SET CompareAtPrice = Price WHERE Id = {0}", id);
    }

    [Fact]
    public async Task SlugsAndSkus_AreUniqueInTheDatabase()
    {
        var (product, other, _) = await ProductsAsync();

        await AssertRejectedAsync("UX_Products_Slug", "UPDATE Products SET Slug = {0} WHERE Id = {1}", product.Slug, other.Id);
        await AssertRejectedAsync("UX_ProductVariants_Sku", "UPDATE ProductVariants SET Sku = {0} WHERE Id = {1}", product.Variants[0].Sku, other.Variants[0].Id);
    }

    [Fact]
    public async Task AttributeValue_OptionMustBelongToItsAttribute()
    {
        var (product, _, attrs) = await ProductsAsync();

        await AssertRejectedAsync("FK_ProductAttributeValues_AttributeOptions_AttributeId_OptionId",
            "INSERT INTO ProductAttributeValues (ProductId, VariantId, AttributeId, OptionId) VALUES ({0}, NULL, {1}, {2})",
            product.Id, attrs["wattage"].Id, attrs["cct"].Options[0].Id);
    }

    [Fact]
    public async Task AttributeValue_VariantMustBelongToItsProduct()
    {
        var (product, other, attrs) = await ProductsAsync();

        await AssertRejectedAsync("FK_ProductAttributeValues_ProductVariants_ProductId_VariantId",
            "INSERT INTO ProductAttributeValues (ProductId, VariantId, AttributeId, OptionId) VALUES ({0}, {1}, {2}, {3})",
            product.Id, other.Variants[0].Id, attrs["wattage"].Id, attrs["wattage"].Options[0].Id);
    }

    [Fact]
    public async Task AttributeValue_OnePerAttributeAtProductLevel_AndOptionXorText()
    {
        var (product, _, attrs) = await ProductsAsync();
        const string option = "INSERT INTO ProductAttributeValues (ProductId, VariantId, AttributeId, OptionId) VALUES ({0}, NULL, {1}, {2})";
        var baseAttr = attrs["base"];
        await SqlAsync(option, product.Id, baseAttr.Id, baseAttr.Options[0].Id);

        // VariantId NULL twice: SQL Server's unique index treats the NULLs as equal, which is the rule we want.
        await AssertRejectedAsync("UX_ProductAttributeValues_ProductId_VariantId_AttributeId", option,
            product.Id, baseAttr.Id, baseAttr.Options[1].Id);
        await AssertRejectedAsync("CK_ProductAttributeValues_OptionXorText",
            "INSERT INTO ProductAttributeValues (ProductId, VariantId, AttributeId, OptionId, Text) VALUES ({0}, NULL, {1}, NULL, NULL)",
            product.Id, attrs["lumen"].Id);
        await AssertRejectedAsync("CK_ProductAttributeValues_OptionXorText",
            "INSERT INTO ProductAttributeValues (ProductId, VariantId, AttributeId, OptionId, Text) VALUES ({0}, NULL, {1}, {2}, {3})",
            product.Id, attrs["wattage"].Id, attrs["wattage"].Options[0].Id, "۹ وات");
    }

    [Fact]
    public async Task Category_CannotBeItsOwnParent()
    {
        var categoryId = (await CreateCategoryAsync(await LoginAdminAsync(), Unique("cat"))).Id;

        await AssertRejectedAsync("CK_Categories_NotOwnParent", "UPDATE Categories SET ParentId = Id WHERE Id = {0}", categoryId);
    }
}

public sealed class StockTests(ApiFixture fixture) : CatalogTestBase(fixture)
{
    private async Task<int> VariantWithStockAsync(int stock, bool isActive = true)
    {
        var admin = await LoginAdminAsync();
        var categoryId = (await CreateCategoryAsync(admin, Unique("cat"))).Id;
        var v = (await CreateProductAsync(admin, categoryId, Unique("p"), variants: [(100_000, stock)])).Variants[0];
        if (!isActive)
        {
            await SendOkAsync<AdminVariantBody>(admin, HttpMethod.Put, $"/api/v1/admin/variants/{v.Id}",
                new { v.Sku, v.Price, v.CompareAtPrice, v.Stock, isActive = false, v.SortOrder, v.RowVersion });
        }

        return v.Id;
    }

    /// <summary>Each call gets its own scope/DbContext/connection, like two separate checkout requests.</summary>
    private async Task<bool> DecrementAsync(int variantId, int quantity)
    {
        using var scope = Fixture.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IStockService>().TryDecrementAsync(variantId, quantity, CancellationToken.None);
    }

    private Task<int> StockAsync(int variantId) =>
        Fixture.WithDbAsync(db => db.ProductVariants.AsNoTracking().Where(v => v.Id == variantId).Select(v => v.Stock).SingleAsync());

    [Fact]
    public async Task Decrement_TakesStock_AndNeverGoesBelowZero()
    {
        var id = await VariantWithStockAsync(3);

        Assert.True(await DecrementAsync(id, 2));
        Assert.False(await DecrementAsync(id, 2)); // only 1 left: refused, nothing taken
        Assert.Equal(1, await StockAsync(id));
        Assert.True(await DecrementAsync(id, 1));
        Assert.False(await DecrementAsync(id, 1));
        Assert.Equal(0, await StockAsync(id));
    }

    [Fact]
    public async Task TwoConcurrentOrders_ForTheLastItem_ExactlyOneSucceeds()
    {
        var id = await VariantWithStockAsync(1);

        var results = await Task.WhenAll(DecrementAsync(id, 1), DecrementAsync(id, 1));

        Assert.Single(results, r => r);
        Assert.Equal(0, await StockAsync(id));
    }

    [Fact]
    public async Task ManyConcurrentOrders_ExactlyTheAvailableQuantitySucceeds()
    {
        var id = await VariantWithStockAsync(5);

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => DecrementAsync(id, 1)));

        Assert.Equal(5, results.Count(r => r));
        Assert.Equal(0, await StockAsync(id));
    }

    [Fact]
    public async Task InactiveVariant_CannotBeBought_AndIncrementRestoresStock()
    {
        var inactive = await VariantWithStockAsync(5, isActive: false);
        var active = await VariantWithStockAsync(1);

        Assert.False(await DecrementAsync(inactive, 1));
        Assert.True(await DecrementAsync(active, 1));
        using (var scope = Fixture.Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IStockService>().IncrementAsync(active, 1, CancellationToken.None);
        }

        Assert.Equal(1, await StockAsync(active));
    }
}
