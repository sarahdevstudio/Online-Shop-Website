using Shop.Domain.Security;

namespace Shop.UnitTests;

public class AdminPasswordRulesTests
{
    [Theory]
    [InlineData("k7Qx9mZp2VbN", "owner@shop.test")]
    [InlineData("correct horse battery", "owner@shop.test")]
    [InlineData("Zk3-Vq9_Lm2Xr7", null)]
    public void StrongEnoughPasswords_AreAccepted(string password, string? email) =>
        Assert.Null(AdminPasswordRules.Weakness(password, email));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("elevenchars")] // 11 characters
    public void ShorterThanTwelve_IsRefused(string? password) =>
        Assert.Contains("at least 12", AdminPasswordRules.Weakness(password, "owner@shop.test"));

    [Theory]
    [InlineData("aaaaaaaaaaaaaaaa", "repetitive")]
    [InlineData("abababababababab", "repetitive")]
    [InlineData("1234567890123456", "digits only")] // 10 distinct? only 10 digits, but digits only is the stated reason below
    public void RepetitiveOrDigitsOnly_IsRefused(string password, string _)
    {
        Assert.NotNull(AdminPasswordRules.Weakness(password, "owner@shop.test"));
    }

    [Fact]
    public void PasswordContainingTheEmailName_IsRefused()
    {
        Assert.Contains("e-mail", AdminPasswordRules.Weakness("xx-MyShopOwner-99!", "myshopowner@shop.test"));
        Assert.Null(AdminPasswordRules.Weakness("xx-MyShopOwner-99!", "other@shop.test"));
    }

    [Fact]
    public void TooLong_IsRefused() =>
        Assert.Contains("at most", AdminPasswordRules.Weakness(new string('a', 10) + "B1!" + new string('c', 130), "a@b.test"));
}
