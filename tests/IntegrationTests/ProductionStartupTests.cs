using Microsoft.Extensions.Configuration;
using Shop.Api.Infrastructure;

namespace Shop.IntegrationTests;

/// <summary>Production must refuse to start with a missing/placeholder storefront key or placeholder payment details.</summary>
public class ProductionStartupTests
{
    private const string GoodKey = "k7Qx9mZp2VbN4tLw8RsY1uHd6GfJ3cAe";

    private static IConfiguration Config(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(v => v.Key, v => v.Value)).Build();

    private static readonly (string, string?)[] RealCheckout =
    [
        ("Checkout:CardToCard:CardNumber", "6037-9912-3456-7890"),
        ("Checkout:CardToCard:HolderName", "رضا احمدی"),
    ];

    [Fact]
    public void RealKeyAndPaymentDetails_AreAccepted() =>
        StartupChecks.RequireProductionSettings(Config([("Storefront:ApiKey", GoodKey), .. RealCheckout]));

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
            StartupChecks.RequireProductionSettings(Config([("Storefront:ApiKey", key), .. RealCheckout])));

        Assert.Contains("Storefront:ApiKey", ex.Message);
    }

    [Fact]
    public void PlaceholderCardDetails_StopStartup_WhenCardToCardIsEnabled()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => StartupChecks.RequireProductionSettings(Config(("Storefront:ApiKey", GoodKey))));

        Assert.Contains("CardNumber", ex.Message);
    }

    [Fact]
    public void CardToCardDisabled_NeedsNoCardDetails() =>
        StartupChecks.RequireProductionSettings(Config(("Storefront:ApiKey", GoodKey), ("Checkout:CardToCard:Enabled", "false")));
}
