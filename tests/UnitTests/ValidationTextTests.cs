using FluentValidation;
using Shop.Application.Accounts;
using Shop.Application.Common;
using Shop.Application.Translations;

namespace Shop.UnitTests;

public class ValidationTextTests
{
    public ValidationTextTests() => ValidationText.Configure();

    private static bool IsPersianOnly(string message) =>
        message.Any(c => c is >= '؀' and <= 'ۿ') && !message.Any(char.IsAsciiLetter);

    [Fact]
    public void BuiltInMessages_AreKeysAtTheApplicationLayer_AndPersianOnlyAfterTheBoundary()
    {
        var login = new LoginRequestValidator().Validate(new LoginRequest("", ""));
        var register = new RegisterCustomerRequestValidator().Validate(new RegisterCustomerRequest("not-an-email", "short", "", new string('1', 100)));

        var raw = login.Errors.Concat(register.Errors).Select(e => e.ErrorMessage).ToList();
        Assert.NotEmpty(raw);
        Assert.All(raw, m => Assert.StartsWith("api.validation.", m));

        var text = raw.Select(Translate.Resolve).ToList();
        Assert.All(text, m => Assert.True(IsPersianOnly(m), $"'{m}' is not Persian-only"));
    }

    [Theory]
    [InlineData("Username", "api.field.username")]
    [InlineData("Password", "api.field.password")]
    [InlineData("SomethingNew", "api.field.unknown")]
    [InlineData(null, "api.field.unknown")]
    public void LabelKey_NamesKnownPropertiesAndFallsBack(string? property, string expected) =>
        Assert.Equal(expected, ValidationText.LabelKey(property));

    [Fact]
    public void EveryLabelAndBuiltInMessageKey_IsInTheManifest()
    {
        foreach (var key in ValidationText.LabelKeys.Concat(ValidationText.MessageKeys).Append("api.field.unknown"))
        {
            Assert.True(TranslationManifest.Defaults.ContainsKey(key), $"{key} is missing from translations/fa-IR.json");
        }
    }
}
