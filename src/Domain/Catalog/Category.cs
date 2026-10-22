using Shop.Domain.Common;

namespace Shop.Domain.Catalog;

/// <summary>
/// Catalog category. At most two levels: a category with a parent must not have children of its own
/// (enforced by the admin service, which can see the whole tree).
/// </summary>
public sealed class Category
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 2000;

    private Category()
    {
        Name = Slug = string.Empty;
    }

    public int Id { get; private set; }
    public int? ParentId { get; private set; }
    public string Name { get; private set; }
    public string Slug { get; private set; }
    public string? Description { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    public static Category Create(string name, string slug, int? parentId, string? description, int sortOrder, bool isActive)
    {
        var category = new Category { Slug = Catalog.Slug.Require(slug, nameof(slug)) };
        category.Update(name, parentId, description, sortOrder, isActive);
        return category;
    }

    public void Update(string name, int? parentId, string? description, int sortOrder, bool isActive)
    {
        Name = CatalogText.RequireName(name, NameMaxLength, nameof(name));
        ParentId = parentId;
        Description = CatalogText.OptionalDescription(description, DescriptionMaxLength, nameof(description));
        SortOrder = sortOrder;
        IsActive = isActive;
    }
}

internal static class CatalogText
{
    public static string RequireName(string? value, int maxLength, string paramName)
    {
        var normalized = PersianText.Normalize(value);
        return normalized.Length > 0 && normalized.Length <= maxLength
            ? normalized
            : throw new ArgumentException($"A name of 1-{maxLength} characters is required.", paramName);
    }

    public static string? OptionalDescription(string? value, int maxLength, string paramName)
    {
        var normalized = PersianText.NormalizeLetters(value);
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"Must be at most {maxLength} characters.", paramName);
        }

        return normalized.Length == 0 ? null : normalized;
    }
}
