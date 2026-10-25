using Microsoft.Extensions.Configuration;
using Shop.Api.Infrastructure;

namespace Shop.IntegrationTests;

/// <summary>Production must refuse to start with a missing or placeholder storefront key.</summary>
public class ProductionStartupTests
{
    private const string GoodKey = "k7Qx9mZp2VbN4tLw8RsY1uHd6GfJ3cAe";

    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(v => v.Key, v => v.Value)).Build();

    [Fact]
    public void RealKey_IsAccepted() =>
        StartupChecks.RequireProductionSettings(Config([("Storefront:ApiKey", GoodKey)]));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("too-short")]
    [InlineData("dev-storefront-key-not-a-secret-0123456789")]
    [InlineData("change-me-change-me-change-me-12345678")]
    [InlineData("test-storefront-key-0123456789abcdef-xyz")]
    public void MissingOrPlaceholderKey_StopsStartup(string? key)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            StartupChecks.RequireProductionSettings(Config([("Storefront:ApiKey", key)])));

        Assert.Contains("Storefront:ApiKey", ex.Message);
    }

    [Fact]
    public void SeededAdminPassword_StopsAProductionStart()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            StartupChecks.RequireProductionSettings(Config(("Storefront:ApiKey", GoodKey), ("Seed:AdminPassword", "anything-12345"))));

        Assert.Contains("create-admin", ex.Message);
        Assert.DoesNotContain("anything-12345", ex.Message);
    }

    [Fact]
    public void WithoutASeededPassword_ProductionStarts() =>
        StartupChecks.RequireProductionSettings(Config(("Storefront:ApiKey", GoodKey), ("Seed:AdminEmail", "owner@shop.test")));
}
