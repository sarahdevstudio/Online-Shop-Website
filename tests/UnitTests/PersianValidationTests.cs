using FluentValidation;
using Shop.Application.Accounts;
using Shop.Application.Common;

namespace Shop.UnitTests;

public class PersianValidationTests
{
    public PersianValidationTests() => PersianValidation.Configure();

    private static bool IsPersianOnly(string message) =>
        message.Any(c => c is >= '؀' and <= 'ۿ') && !message.Any(char.IsAsciiLetter);

    [Fact]
    public void BuiltInMessages_AreInPersian_WithPersianPropertyNames()
    {
        var login = new LoginRequestValidator().Validate(new LoginRequest("", ""));
        var register = new RegisterCustomerRequestValidator().Validate(new RegisterCustomerRequest("not-an-email", "short", "", new string('1', 100)));

        var messages = login.Errors.Concat(register.Errors).Select(e => e.ErrorMessage).ToList();
        Assert.NotEmpty(messages);
        Assert.All(messages, m => Assert.True(IsPersianOnly(m), $"'{m}' is not Persian-only"));
    }

    [Theory]
    [InlineData("Username", "نام کاربری")]
    [InlineData("Password", "رمز عبور")]
    [InlineData("SomethingNew", "این مقدار")]
    [InlineData(null, "این مقدار")]
    public void Label_NamesKnownPropertiesAndFallsBack(string? property, string expected) =>
        Assert.Equal(expected, PersianValidation.Label(property));
}
