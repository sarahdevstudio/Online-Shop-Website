using Shop.Domain.Common;

namespace Shop.Application.Catalog.Transfer;

/// <summary>A cell grid read from a CSV or Excel file. <see cref="Number"/> is the row's physical line in the file (header = 1).</summary>
public sealed record TableRow(int Number, IReadOnlyList<string> Cells);

public sealed record TableFile(IReadOnlyList<string> Header, IReadOnlyList<TableRow> Rows);

/// <summary>A file that cannot be read at all (not a CSV/XLSX, too big, damaged). The message is a translation key.</summary>
public sealed class TableFileException(string message) : Exception(message)
{
    public static TableFileException Of(string key, params (string Name, object? Value)[] args) => new(Msg.Of(key, args));
}

/// <summary>Size limits for uploaded tables. Generous for a lighting catalog, small enough that a hostile file cannot hurt.</summary>
public static class TableLimits
{
    public const int MaxFileBytes = 2 * 1024 * 1024;
    public const int MaxUncompressedBytes = 20 * 1024 * 1024;
    public const int MaxRows = 2000;
    public const int MaxColumns = 80;
    public const int MaxCellLength = 10_000;
    public const int MaxZipEntries = 100;
}
