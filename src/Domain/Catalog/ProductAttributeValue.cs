namespace Shop.Domain.Catalog;

/// <summary>
/// One attribute value of a product. <see cref="VariantId"/> null = a spec of the whole product (base E27);
/// set = a variant axis (this variant is 9W). Exactly one of <see cref="OptionId"/> / <see cref="Text"/> is set.
/// </summary>
public sealed class ProductAttributeValue
{
    public const int TextMaxLength = 200;

    private ProductAttributeValue()
    {
    }

    public int Id { get; private set; }
    public int ProductId { get; private set; }
    public int? VariantId { get; private set; }
    public int AttributeId { get; private set; }
    public int? OptionId { get; private set; }
    public string? Text { get; private set; }

    public ProductVariant? Variant { get; private set; }

    public static ProductAttributeValue ForOption(int attributeId, int optionId, int? variantId) =>
        new() { AttributeId = attributeId, OptionId = optionId, VariantId = variantId };

    public static ProductAttributeValue ForText(int attributeId, string text, int? variantId) =>
        new() { AttributeId = attributeId, Text = CatalogText.RequireName(text, TextMaxLength, nameof(text)), VariantId = variantId };
}
