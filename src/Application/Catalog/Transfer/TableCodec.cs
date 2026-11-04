namespace Shop.Application.Catalog.Transfer;

/// <summary>Reads an uploaded CSV or Excel file and writes the export in either format.</summary>
public static class TableCodec
{
    public static TableFile Read(Stream content, string fileName)
    {
        if (content.CanSeek && content.Length > TableLimits.MaxFileBytes)
        {
            throw TableFileException.Of("api.import.file-too-large");
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = content.Read(chunk, 0, chunk.Length)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > TableLimits.MaxFileBytes)
            {
                throw TableFileException.Of("api.import.file-too-large");
            }
        }

        var bytes = buffer.ToArray();
        var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        var isZip = bytes.Length > 3 && bytes[0] == 'P' && bytes[1] == 'K';
        return (extension, isZip) switch
        {
            (".xlsx", true) => XlsxTable.Read(bytes),
            (".csv", false) => CsvTable.Read(bytes),
            (".xlsx", false) or (".csv", true) => throw TableFileException.Of("api.import.file-type-mismatch"),
            _ => throw TableFileException.Of("api.import.file-type"),
        };
    }

    public static byte[] Write(string format, IReadOnlyList<string> header, IEnumerable<IReadOnlyList<string>> rows, ISet<int> numericColumns) =>
        format == "xlsx" ? XlsxTable.Write(header, rows, numericColumns, "products") : CsvTable.Write(header, rows);
}
