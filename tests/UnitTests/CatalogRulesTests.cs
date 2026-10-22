using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Shop.Application.Catalog;
using Shop.Domain.Catalog;
using Shop.Infrastructure.Catalog;

namespace Shop.UnitTests;

public class SlugTests
{
    [Theory]
    [InlineData("lamps")]
    [InlineData("led-bulb-9w-e27-warm")]
    [InlineData("3000k")]
    [InlineData("a")]
    public void Valid(string slug)
    {
        Assert.True(Slug.IsValid(slug));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Lamps")] // upper-case
    [InlineData("led bulb")] // space
    [InlineData("led_bulb")] // underscore
    [InlineData("-led")] // leading hyphen
    [InlineData("led-")] // trailing hyphen
    [InlineData("led--bulb")] // double hyphen
    [InlineData("لامپ")] // not English
    [InlineData("led/bulb")]
    public void Invalid(string? slug)
    {
        Assert.False(Slug.IsValid(slug));
    }

    [Fact]
    public void MaxLength_IsEnforced()
    {
        Assert.True(Slug.IsValid(new string('a', Slug.MaxLength)));
        Assert.False(Slug.IsValid(new string('a', Slug.MaxLength + 1)));
    }

    [Fact]
    public void Entities_RejectInvalidSlugs()
    {
        Assert.Throws<ArgumentException>(() => Brand.Create("زر", "Zar Brand", true));
        Assert.Throws<ArgumentException>(() => Category.Create("لامپ", "lamps!", null, null, 0, true));
    }

    [Fact]
    public void Validators_RejectInvalidSlugs()
    {
        var result = new CategoryRequestValidator().Validate(new CategoryRequest("لامپ", "Lamps", null, null, 0, true));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CategoryRequest.Slug));
    }
}

public class CatalogEntityTests
{
    [Fact]
    public void Names_AreStoredNormalized()
    {
        var brand = Brand.Create("  پارس  شهاب ", "pars-shahab", true);
        var product = Product.Create(1, 1, brand.Name, "لامپ كم‌ مصرف ۹ وات", "lamp-9w", null, ProductStatus.Draft, DateTime.UtcNow);

        Assert.Equal("پارس شهاب", brand.Name);
        Assert.Equal("لامپ کم مصرف ۹ وات", product.Name);
        Assert.Equal("لامپ کم مصرف 9 وات پارس شهاب", product.SearchKey);
    }

    [Fact]
    public void Name_OfOnlyWhitespaceAndZwnj_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => Brand.Create(" ‌ ", "x", true));
        Assert.False(new BrandRequestValidator().Validate(new BrandRequest(" ‌ ", "x", true)).IsValid);
    }

    [Theory]
    [InlineData(0, null, 1)] // price must be positive
    [InlineData(100, 100L, 1)] // compare-at must be higher than price
    [InlineData(100, null, -1)] // stock can't be negative
    public void Variant_RejectsInvalidMoneyAndStock(long price, long? compareAt, int stock)
    {
        Assert.ThrowsAny<ArgumentException>(() => ProductVariant.Create("SKU-1", price, compareAt, stock, true, 0));
        Assert.False(new VariantRequestValidator().Validate(new VariantRequest("SKU-1", price, compareAt, stock, true, 0)).IsValid);
    }

    [Theory]
    [InlineData("PS-LB-9W-3000", true)]
    [InlineData("sku.1_a", true)]
    [InlineData("-SKU", false)]
    [InlineData("SKU 1", false)]
    [InlineData("", false)]
    public void Sku_Format(string sku, bool valid)
    {
        Assert.Equal(valid, ProductVariant.IsValidSku(sku));
    }

    [Fact]
    public void OnlyOptionAttributes_CanBeFilterable()
    {
        Assert.Throws<ArgumentException>(() => AttributeDefinition.Create("lumen", "شار نوری", null, AttributeType.Text, true, 0));
    }
}

public class MediaOptionsTests
{
    private static readonly string App = Path.Combine(Path.GetTempPath(), "shop-app", "bin");

    [Theory]
    [InlineData("media")] // inside the binary folder (a published app's default)
    [InlineData("images/products")]
    [InlineData("")] // the binary folder itself
    [InlineData("..")] // a parent of the binary folder
    public void MediaRoot_OverlappingTheApplicationFolder_IsRefused(string relative)
    {
        Assert.Throws<InvalidOperationException>(() => MediaOptions.EnsureOutsideApplication(Path.Combine(App, relative), App));
    }

    [Theory]
    [InlineData("../../shop-media")]
    [InlineData("../bin-media")] // sibling whose name merely starts with the same characters
    public void MediaRoot_OutsideTheApplicationFolder_IsAccepted(string relative)
    {
        MediaOptions.EnsureOutsideApplication(Path.GetFullPath(Path.Combine(App, relative)), App);
    }

    [Fact]
    public void DefaultLimit_IsFiveMegabytes()
    {
        Assert.Equal(5 * 1024 * 1024, new MediaOptions().MaxImageBytes);
    }
}

public class CatalogSeedRulesTests
{
    [Fact]
    public void BuiltInAttributeCodes_AreValid_AndDoNotCollideWithReservedQueryKeys()
    {
        Assert.Contains("wattage", CatalogSeeder.AttributeCodes);
        Assert.Contains("base", CatalogSeeder.AttributeCodes);
        Assert.Contains("cct", CatalogSeeder.AttributeCodes);
        Assert.All(CatalogSeeder.AttributeCodes, code =>
        {
            Assert.True(AttributeDefinition.IsValidCode(code));
            Assert.DoesNotContain(code, ProductListQuery.ReservedKeys);
        });
    }

    [Theory]
    [InlineData("Development", true)]
    [InlineData("Testing", false)]
    [InlineData("Staging", false)]
    [InlineData("Production", false)]
    public void SampleData_IsSeededInDevelopmentOnly(string environment, bool expected)
    {
        Assert.Equal(expected, CatalogSeeder.ShouldSeedSampleData(new FakeEnvironment(environment)));
    }

    private sealed class FakeEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Shop.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
