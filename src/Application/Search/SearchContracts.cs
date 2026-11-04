namespace Shop.Application.Search;

public sealed record SuggestProductDto(string Slug, string Name, string Path, long Price, string? ImageUrl);

public sealed record SuggestCategoryDto(string Slug, string Name, string Path);

/// <summary><see cref="Fuzzy"/>: nothing matched exactly, these are the nearest spellings.</summary>
public sealed record SuggestDto(IReadOnlyList<SuggestProductDto> Products, IReadOnlyList<SuggestCategoryDto> Categories, bool Fuzzy);

/// <summary>
/// The catalog search by typing: suggestions for the header box, and typo-tolerant product ids for the results page. Both run over an
/// in-memory index of the public catalog that is rebuilt on the next query after any catalog change.
/// </summary>
public interface ICatalogSearch
{
    Task<SuggestDto> SuggestAsync(string? query, CancellationToken ct);

    /// <summary>Ids of the published products whose name (or brand) is close to the query, best first; empty when nothing is close.</summary>
    Task<IReadOnlyList<int>> FuzzyProductIdsAsync(string? query, int limit, CancellationToken ct);

    /// <summary>Something in the catalog changed: rebuild before the next query.</summary>
    void MarkChanged();
}
