using Shop.Application.Catalog;
using Shop.Application.Common;

namespace Shop.Application.Translations;

public sealed record AdminTranslationDto(
    int Id,
    string Key,
    string Culture,
    string Text,
    string DefaultText,
    bool IsEdited,
    /// <summary>False when the code no longer uses the key (it was removed from the manifest).</summary>
    bool InUse,
    IReadOnlyList<string> Placeholders,
    DateTime UpdatedAtUtc,
    string? UpdatedBy,
    byte[] RowVersion);

public sealed record TranslationAreaDto(string Area, int Count, int Edited);

public sealed record TranslationAuditEntryDto(DateTime AtUtc, string? AdminUsername, string OldText, string NewText);

public sealed record UpdateTranslationRequest(string Text, byte[] RowVersion);

public sealed record ResetTranslationRequest(byte[] RowVersion);

public interface IAdminTranslationService
{
    /// <param name="q">Matches the key or the current / default text.</param>
    /// <param name="area">A prefix: <c>shop</c> or <c>shop.cart</c>.</param>
    Task<PagedList<AdminTranslationDto>> ListAsync(string? q, string? area, bool editedOnly, int page, CancellationToken ct);

    Task<IReadOnlyList<TranslationAreaDto>> AreasAsync(CancellationToken ct);

    Task<Result<AdminTranslationDto>> GetAsync(int id, CancellationToken ct);

    Task<Result<AdminTranslationDto>> UpdateAsync(int id, UpdateTranslationRequest request, CancellationToken ct);

    Task<Result<AdminTranslationDto>> ResetAsync(int id, ResetTranslationRequest request, CancellationToken ct);

    Task<Result<IReadOnlyList<TranslationAuditEntryDto>>> AuditAsync(int id, CancellationToken ct);
}
