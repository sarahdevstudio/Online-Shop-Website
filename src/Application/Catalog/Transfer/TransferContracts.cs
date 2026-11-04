using Shop.Application.Common;

namespace Shop.Application.Catalog.Transfer;

/// <summary>A problem found in one row (<see cref="Row"/> = the line in the file; 0 = the header). The message is finished Persian text.</summary>
public sealed record ImportRowError(int Row, string Column, string Message);

/// <summary>What the file would do to one row: create-product, create-variant, update-variant or unchanged.</summary>
public sealed record ImportRowPreview(int Row, string ProductSlug, string Sku, string Action);

public sealed record ImportSummary(int Rows, int ProductsToCreate, int ProductsToUpdate, int VariantsToCreate, int VariantsToUpdate, int Unchanged);

/// <summary>The dry run: nothing has been written. <see cref="CanApply"/> is false while there are errors.</summary>
public sealed record ImportPreviewDto(
    bool CanApply, ImportSummary Summary, IReadOnlyList<ImportRowPreview> Rows, IReadOnlyList<ImportRowError> Errors, bool ErrorsTruncated);

public sealed record ImportAppliedDto(ImportSummary Summary);

public sealed record ExportFile(string FileName, string ContentType, byte[] Content);

/// <summary>Products as a CSV / Excel table and back (docs/16). One row per variant; export and import share the same columns.</summary>
public interface ICatalogTransferService
{
    Task<ExportFile> ExportAsync(string format, CancellationToken ct);

    /// <summary>The dry run: reads the file and reports what it would create and update, and every problem per row. Writes nothing.</summary>
    Task<Result<ImportPreviewDto>> PreviewAsync(Stream file, string fileName, CancellationToken ct);

    /// <summary>Applies the file in ONE transaction: any problem in any row means nothing is written.</summary>
    Task<Result<ImportAppliedDto>> ApplyAsync(Stream file, string fileName, CancellationToken ct);
}
