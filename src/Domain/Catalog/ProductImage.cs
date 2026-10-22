namespace Shop.Domain.Catalog;

/// <summary>Gallery image. <see cref="Path"/> is relative to the media root (served under <c>/media/</c>).</summary>
public sealed class ProductImage
{
    public const int PathMaxLength = 300;
    public const int AltMaxLength = 200;

    private ProductImage()
    {
        Path = Alt = string.Empty;
    }

    public int Id { get; private set; }
    public int ProductId { get; private set; }
    public string Path { get; private set; }
    public string Alt { get; private set; }
    public int SortOrder { get; private set; }

    public static ProductImage Create(string path, string alt, int sortOrder) => new()
    {
        Path = path,
        Alt = CatalogText.RequireName(alt, AltMaxLength, nameof(alt)),
        SortOrder = sortOrder,
    };

    public void MoveTo(int sortOrder) => SortOrder = sortOrder;
}
