using System.Globalization;
using FluentValidation;
using FluentValidation.Resources;
using Shop.Domain.Common;

namespace Shop.Application.Common;

/// <summary>
/// FluentValidation's built-in messages and property names would be English (or text the translation table cannot edit). This makes
/// the library produce message keys instead: each built-in message becomes <c>api.validation.*|PropertyName=api.field.*|...</c>,
/// the API boundary turns that into the table's text, so an edit in the translation editor applies to them too.
/// Property names are label keys (<c>api.field.mobile</c>), also resolved at the boundary.
/// </summary>
public static class ValidationText
{
    private const string UnknownLabelKey = "api.field.unknown";

    /// <summary>Library message id → table key; the placeholders in the table text are the library's own ({PropertyName}, {MaxLength}...).</summary>
    private static readonly Dictionary<string, string> Keys = new(StringComparer.Ordinal)
    {
        ["NotEmptyValidator"] = "api.validation.not-empty",
        ["NotNullValidator"] = "api.validation.not-empty",
        ["MaximumLengthValidator"] = "api.validation.max-length-total",
        ["MinimumLengthValidator"] = "api.validation.min-length-total",
        ["LengthValidator"] = "api.validation.length-total",
        ["ExactLengthValidator"] = "api.validation.exact-length-total",
        ["InclusiveBetweenValidator"] = "api.validation.between",
        ["ExclusiveBetweenValidator"] = "api.validation.exclusive-between",
        ["GreaterThanValidator"] = "api.validation.greater-than",
        ["GreaterThanOrEqualValidator"] = "api.validation.greater-than-or-equal",
        ["LessThanValidator"] = "api.validation.less-than",
        ["LessThanOrEqualValidator"] = "api.validation.less-than-or-equal",
        ["EmailValidator"] = "api.validation.email",
        ["RegularExpressionValidator"] = "api.validation.format",
        ["PredicateValidator"] = "api.validation.predicate",
        ["AsyncPredicateValidator"] = "api.validation.predicate",
        ["ScalePrecisionValidator"] = "api.validation.scale-precision",
        ["EqualValidator"] = "api.validation.equal",
        ["NotEqualValidator"] = "api.validation.not-equal",
        ["EnumValidator"] = "api.validation.enum",
        ["NullValidator"] = "api.validation.must-be-empty",
        ["EmptyValidator"] = "api.validation.must-be-empty",
        ["CreditCardValidator"] = "api.validation.credit-card",
        ["Length_Simple"] = "api.validation.length",
        ["MinimumLength_Simple"] = "api.validation.min-length",
        ["MaximumLength_Simple"] = "api.validation.max-length",
        ["ExactLength_Simple"] = "api.validation.exact-length",
        ["InclusiveBetween_Simple"] = "api.validation.between-simple",
    };

    /// <summary>The placeholders each library message offers, passed on as named arguments of the key.</summary>
    private static readonly string[] Placeholders =
        ["PropertyName", "MaxLength", "MinLength", "TotalLength", "From", "To", "PropertyValue", "ComparisonValue", "ExpectedPrecision", "ExpectedScale", "Digits", "ActualScale"];

    private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
    {
        ["Address"] = "api.field.address",
        ["CardLast"] = "api.field.card-last",
        ["CategoryId"] = "api.field.category",
        ["City"] = "api.field.city",
        ["CompareAtPrice"] = "api.field.compare-at-price",
        ["CurrentPassword"] = "api.field.current-password",
        ["Description"] = "api.field.description",
        ["Email"] = "api.field.email",
        ["ExpectedTotal"] = "api.field.expected-total",
        ["FullName"] = "api.field.full-name",
        ["ImageIds"] = "api.field.images",
        ["Mobile"] = "api.field.mobile",
        ["Name"] = "api.field.name",
        ["NewPassword"] = "api.field.new-password",
        ["Note"] = "api.field.note",
        ["Password"] = "api.field.password",
        ["PaymentMethod"] = "api.field.payment-method",
        ["Phone"] = "api.field.phone",
        ["PostalCode"] = "api.field.postal-code",
        ["Price"] = "api.field.price",
        ["Province"] = "api.field.province",
        ["Quantity"] = "api.field.quantity",
        ["RowVersion"] = "api.field.row-version",
        ["Sku"] = "api.field.sku",
        ["Slug"] = "api.field.slug",
        ["Status"] = "api.field.status",
        ["Stock"] = "api.field.stock",
        ["Time"] = "api.field.time",
        ["Tracking"] = "api.field.tracking",
        ["Username"] = "api.field.username",
        ["Value"] = "api.field.value",
        ["Values"] = "api.field.values",
        ["VariantId"] = "api.field.variant",
        ["Variants"] = "api.field.variants",
    };

    public static IReadOnlyCollection<string> LabelKeys => Labels.Values;

    public static IReadOnlyCollection<string> MessageKeys => Keys.Values.Distinct().ToList();

    public static string LabelKey(string? property) => property is not null && Labels.TryGetValue(property, out var key) ? key : UnknownLabelKey;

    public static void Configure()
    {
        ValidatorOptions.Global.LanguageManager = new KeyedLanguageManager();
        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("fa");
        ValidatorOptions.Global.DisplayNameResolver = (_, member, _) => LabelKey(member?.Name);
    }

    private sealed class KeyedLanguageManager : LanguageManager
    {
        public override string GetString(string key, CultureInfo? culture = null)
        {
            if (!Keys.TryGetValue(key, out var messageKey))
            {
                return base.GetString(key, culture);
            }

            // "key|PropertyName={PropertyName}|MaxLength={MaxLength}|..." - the library fills the braces in; unknown ones stay and are ignored.
            return messageKey + string.Concat(Placeholders.Select(p => $"|{p}={{{p}}}"));
        }
    }
}
