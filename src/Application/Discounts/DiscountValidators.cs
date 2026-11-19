using FluentValidation;
using Shop.Domain.Discounts;

namespace Shop.Application.Discounts;

public sealed class ApplyDiscountRequestValidator : AbstractValidator<ApplyDiscountRequest>
{
    public ApplyDiscountRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(DiscountCode.CodeMaxLength);
    }
}
