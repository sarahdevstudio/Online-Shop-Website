using Shop.Application.Accounts;

namespace Shop.UnitTests;

public class AccountValidatorTests
{
    private readonly RegisterCustomerRequestValidator _register = new();
    private readonly LoginRequestValidator _login = new();

    private static RegisterCustomerRequest ValidRegistration() =>
        new("sara@example.com", new string('a', PasswordRules.MinLength), "Sara Ahmadi", null);

    [Fact]
    public void Register_ValidRequest_Passes()
    {
        Assert.True(_register.Validate(ValidRegistration()).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("missing@tld@example.com")]
    public void Register_InvalidEmail_Fails(string email)
    {
        var result = _register.Validate(ValidRegistration() with { Email = email });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterCustomerRequest.Email));
    }

    [Fact]
    public void Register_PasswordShorterThanMinimum_Fails()
    {
        var shortPassword = new string('a', PasswordRules.MinLength - 1);

        var result = _register.Validate(ValidRegistration() with { Password = shortPassword });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterCustomerRequest.Password));
    }

    [Fact]
    public void Register_PasswordLongerThanMaximum_Fails()
    {
        var result = _register.Validate(ValidRegistration() with { Password = new string('a', PasswordRules.MaxLength + 1) });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterCustomerRequest.Password));
    }

    [Fact]
    public void Register_MissingFullName_Fails()
    {
        var result = _register.Validate(ValidRegistration() with { FullName = " " });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterCustomerRequest.FullName));
    }

    [Fact]
    public void Login_EmptyFields_Fail()
    {
        var result = _login.Validate(new LoginRequest("", ""));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LoginRequest.Email));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LoginRequest.Password));
    }
}
