namespace Shop.Domain.Catalog;

public sealed class Brand
{
    public const int NameMaxLength = 100;

    private Brand()
    {
        Name = Slug = string.Empty;
    }

    public int Id { get; private set; }
    public string Name { get; private set; }
    public string Slug { get; private set; }
    public bool IsActive { get; private set; }

    public static Brand Create(string name, string slug, bool isActive)
    {
        var brand = new Brand { Slug = Catalog.Slug.Require(slug, nameof(slug)) };
        brand.Update(name, isActive);
        return brand;
    }

    public void Update(string name, bool isActive)
    {
        Name = CatalogText.RequireName(name, NameMaxLength, nameof(name));
        IsActive = isActive;
    }
}
