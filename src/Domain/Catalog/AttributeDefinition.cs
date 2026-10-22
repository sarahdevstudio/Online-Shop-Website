using System.Text.RegularExpressions;

namespace Shop.Domain.Catalog;

public enum AttributeType
{
    /// <summary>Value is one of <see cref="AttributeDefinition.Options"/>; filterable.</summary>
    Option = 0,

    /// <summary>Free text, display only (lumen, lifetime, ...).</summary>
    Text = 1,
}

/// <summary>
/// A lighting spec or variant axis (wattage, base, cct, ...). Its <see cref="Code"/> is the query-string key used
/// for filtering, so it can't collide with the catalog's reserved query keys.
/// </summary>
public sealed partial class AttributeDefinition
{
    public const int CodeMaxLength = 32;
    public const int NameMaxLength = 100;
    public const int UnitMaxLength = 20;

    private AttributeDefinition()
    {
        Code = Name = string.Empty;
    }

    public int Id { get; private set; }
    public string Code { get; private set; }
    public string Name { get; private set; }
    public string? Unit { get; private set; }
    public AttributeType Type { get; private set; }
    public bool IsFilterable { get; private set; }
    public int SortOrder { get; private set; }
    public List<AttributeOption> Options { get; private set; } = [];

    public static bool IsValidCode(string? code) => code is { Length: > 0 and <= CodeMaxLength } && CodePattern().IsMatch(code);

    public static AttributeDefinition Create(string code, string name, string? unit, AttributeType type, bool isFilterable, int sortOrder)
    {
        if (!IsValidCode(code))
        {
            throw new ArgumentException($"'{code}' is not a valid attribute code.", nameof(code));
        }

        if (isFilterable && type != AttributeType.Option)
        {
            throw new ArgumentException("Only option attributes can be filterable.", nameof(isFilterable));
        }

        return new AttributeDefinition
        {
            Code = code,
            Name = CatalogText.RequireName(name, NameMaxLength, nameof(name)),
            Unit = string.IsNullOrWhiteSpace(unit) ? null : CatalogText.RequireName(unit, UnitMaxLength, nameof(unit)),
            Type = type,
            IsFilterable = isFilterable,
            SortOrder = sortOrder,
        };
    }

    [GeneratedRegex("^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();
}

/// <summary>An allowed value of an option attribute: display label (Persian) + English slug for URLs.</summary>
public sealed class AttributeOption
{
    public const int ValueMaxLength = 100;

    private AttributeOption()
    {
        Value = Slug = string.Empty;
    }

    public int Id { get; private set; }
    public int AttributeId { get; private set; }
    public string Value { get; private set; }
    public string Slug { get; private set; }
    public int SortOrder { get; private set; }

    public static AttributeOption Create(string value, string slug, int sortOrder) => new()
    {
        Value = CatalogText.RequireName(value, ValueMaxLength, nameof(value)),
        Slug = Catalog.Slug.Require(slug, nameof(slug)),
        SortOrder = sortOrder,
    };
}
