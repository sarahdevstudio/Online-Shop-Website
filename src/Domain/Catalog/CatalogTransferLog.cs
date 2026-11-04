namespace Shop.Domain.Catalog;

/// <summary>One export or applied import of the product table: who, when, which file, how much changed.</summary>
public sealed class CatalogTransferLog
{
    public const string ExportKind = "export";
    public const string ImportKind = "import";
    public const int FileNameMaxLength = 200;

    private CatalogTransferLog()
    {
        Kind = FileName = string.Empty;
    }

    public int Id { get; private set; }
    public DateTime AtUtc { get; private set; }
    public Guid? UserId { get; private set; }
    public string Kind { get; private set; }
    public string FileName { get; private set; }
    public int Rows { get; private set; }
    public int ProductsCreated { get; private set; }
    public int ProductsUpdated { get; private set; }
    public int VariantsCreated { get; private set; }
    public int VariantsUpdated { get; private set; }

    public static CatalogTransferLog Create(
        Guid? userId, string kind, string fileName, int rows, int productsCreated, int productsUpdated, int variantsCreated, int variantsUpdated, DateTime utcNow) => new()
    {
        AtUtc = utcNow,
        UserId = userId,
        Kind = kind,
        FileName = fileName.Length > FileNameMaxLength ? fileName[..FileNameMaxLength] : fileName,
        Rows = rows,
        ProductsCreated = productsCreated,
        ProductsUpdated = productsUpdated,
        VariantsCreated = variantsCreated,
        VariantsUpdated = variantsUpdated,
    };
}
