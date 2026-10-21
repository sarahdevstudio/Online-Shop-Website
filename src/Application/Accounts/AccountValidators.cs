using FluentValidation;
using Shop.Domain.Customers;

namespace Shop.Application.Accounts;

public static class PasswordRules
{
    public const int MinLength = 10;
    public const int MaxLength = 128;
}

public sealed class RegisterCustomerRequestValidator : AbstractValidator<RegisterCustomerRequest>
{
    public RegisterCustomerRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256).EmailAddress();
        RuleFor(x => x.Password).NotEmpty()
            .MinimumLength(PasswordRules.MinLength)
            .MaximumLength(PasswordRules.MaxLength);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(Customer.FullNameMaxLength);
        RuleFor(x => x.Phone).MaximumLength(Customer.PhoneMaxLength);
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(PasswordRules.MaxLength);
    }
}
