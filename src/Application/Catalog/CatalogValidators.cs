using FluentValidation;
using Shop.Domain.Catalog;
using Shop.Domain.Common;

namespace Shop.Application.Catalog;

internal static class CatalogRules
{
    /// <summary>Length is checked after normalization, so a name of only spaces/ZWNJ is empty.</summary>
    public static IRuleBuilderOptions<T, string?> PersianName<T>(this IRuleBuilder<T, string?> rule, int maxLength) =>
        rule.Must(v => PersianText.Normalize(v).Length is var n && n > 0 && n <= maxLength)
            .WithMessage($"Required, at most {maxLength} characters.");

    public static IRuleBuilderOptions<T, string?> ValidSlug<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(Slug.IsValid)
            .WithMessage($"Lower-case English letters and digits separated by single hyphens, at most {Slug.MaxLength} characters.");
}

public sealed class CategoryRequestValidator : AbstractValidator<CategoryRequest>
{
    public CategoryRequestValidator()
    {
        RuleFor(x => x.Name).PersianName(Category.NameMaxLength);
        RuleFor(x => x.Description).MaximumLength(Category.DescriptionMaxLength);
        // Slug is only read on create (the service rejects a missing one there); validate its shape when present.
        RuleFor(x => x.Slug).ValidSlug().When(x => x.Slug is not null);
    }
}

public sealed class BrandRequestValidator : AbstractValidator<BrandRequest>
{
    public BrandRequestValidator()
    {
        RuleFor(x => x.Name).PersianName(Brand.NameMaxLength);
        RuleFor(x => x.Slug).ValidSlug().When(x => x.Slug is not null);
    }
}

public sealed class AttributeOptionRequestValidator : AbstractValidator<AttributeOptionRequest>
{
    public AttributeOptionRequestValidator()
    {
        RuleFor(x => x.Value).PersianName(AttributeOption.ValueMaxLength);
        RuleFor(x => x.Slug).ValidSlug();
    }
}

public sealed class VariantFieldsValidator : AbstractValidator<IVariantFields>
{
    public VariantFieldsValidator()
    {
        RuleFor(x => x.Sku).Must(ProductVariant.IsValidSku)
            .WithMessage($"Letters, digits, '.', '_' or '-', at most {ProductVariant.SkuMaxLength} characters.");
        RuleFor(x => x.Price).InclusiveBetween(1, ProductVariant.MaxPrice);
        RuleFor(x => x.CompareAtPrice).Must((x, c) => c is null || (c > x.Price && c <= ProductVariant.MaxPrice))
            .WithMessage("Must be higher than the price.");
        RuleFor(x => x.Stock).InclusiveBetween(0, ProductVariant.MaxStock);
    }
}

public sealed class VariantRequestValidator : AbstractValidator<VariantRequest>
{
    public VariantRequestValidator() => Include(new VariantFieldsValidator());
}

public sealed class UpdateVariantRequestValidator : AbstractValidator<UpdateVariantRequest>
{
    public UpdateVariantRequestValidator()
    {
        Include(new VariantFieldsValidator());
        RuleFor(x => x.RowVersion).NotEmpty();
    }
}

public sealed class ProductRequestValidator : AbstractValidator<ProductRequest>
{
    public ProductRequestValidator()
    {
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.Name).PersianName(Product.NameMaxLength);
        RuleFor(x => x.Description).MaximumLength(Product.DescriptionMaxLength);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Slug).ValidSlug().When(x => x.Slug is not null);
        RuleForEach(x => x.Variants).SetValidator(new VariantRequestValidator());
    }
}

public sealed class ProductAttributesRequestValidator : AbstractValidator<ProductAttributesRequest>
{
    public ProductAttributesRequestValidator()
    {
        RuleFor(x => x.Values).NotNull();
        RuleForEach(x => x.Values).ChildRules(value =>
        {
            value.RuleFor(v => v).Must(v => (v.OptionId is null) != (v.Text is null))
                .WithMessage("Exactly one of optionId or text is required.");
            value.RuleFor(v => v.Text).PersianName(ProductAttributeValue.TextMaxLength).When(v => v.Text is not null);
        });
    }
}

public sealed class ImageOrderRequestValidator : AbstractValidator<ImageOrderRequest>
{
    public ImageOrderRequestValidator()
    {
        RuleFor(x => x.ImageIds).NotNull().Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("Ids must be unique.");
    }
}
