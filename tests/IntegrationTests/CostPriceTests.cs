namespace Shop.IntegrationTests;

/// <summary>
/// F2 (docs/19): CostPrice is admin-only. It must round-trip through the admin API but never appear anywhere a
/// shopper's browser can read it — the public product endpoints (detail and list).
/// </summary>
public sealed class CostPriceTests(ApiFixture fixture) : CatalogTestBase(fixture)
{
    [Fact]
    public async Task CostPrice_RoundTripsThroughTheAdminApi_ButNeverAppearsInAPublicResponse()
    {
        var admin = await LoginAdminAsync();
        var categoryId = (await CreateCategoryAsync(admin, Unique("cat"))).Id;
        var slug = Unique("cost");

        var product = await SendOkAsync<AdminProductBody>(admin, HttpMethod.Post, "/api/v1/admin/products", new
        {
            categoryId,
            brandId = (int?)null,
            name = "محصول " + slug,
            slug,
            description = "توضیح",
            status = "published",
            variants = new[]
            {
                new { sku = slug, price = 200_000L, compareAtPrice = (long?)null, stock = 5, isActive = true, sortOrder = 0, costPrice = 120_000L },
            },
        });

        var variant = product.Variants.Single();

        // Round-trips through the admin API.
        var adminDetail = await admin.GetAsync($"/api/v1/admin/products/{product.Id}");
        var adminBody = await adminDetail.Content.ReadAsStringAsync();
        Assert.Contains("\"costPrice\":120000", adminBody);

        // Updating it keeps the admin-only field working the same way as price/stock.
        var updated = await SendOkAsync<AdminVariantBody>(admin, HttpMethod.Put, $"/api/v1/admin/variants/{variant.Id}", new
        {
            sku = variant.Sku, price = variant.Price, compareAtPrice = variant.CompareAtPrice, stock = variant.Stock,
            isActive = variant.IsActive, sortOrder = variant.SortOrder, rowVersion = variant.RowVersion, costPrice = 130_000L,
        });
        Assert.Equal(130_000L, updated.CostPrice);

        // Never in the public detail or list response.
        var publicDetail = await NewClient().GetAsync($"/api/v1/catalog/products/{slug}");
        var detailBody = await publicDetail.Content.ReadAsStringAsync();
        Assert.DoesNotContain("costPrice", detailBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("130000", detailBody);
        Assert.DoesNotContain("120000", detailBody);

        var publicList = await NewClient().GetAsync($"/api/v1/catalog/products?q={slug}");
        var listBody = await publicList.Content.ReadAsStringAsync();
        Assert.DoesNotContain("costPrice", listBody, StringComparison.OrdinalIgnoreCase);
    }
}
