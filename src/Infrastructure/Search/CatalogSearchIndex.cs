using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shop.Application.Routing;
using Shop.Application.Search;
using Shop.Domain.Catalog;
using Shop.Domain.Common;
using Shop.Domain.Routing;
using Shop.Infrastructure.Persistence;

namespace Shop.Infrastructure.Search;

/// <summary>
/// The in-memory index behind the search box. It holds the name key of every published product and active category (a few thousand
/// short strings at most), is rebuilt lazily on the first query after <see cref="MarkChanged"/> (a catalog save), and matches
/// each query word either as a part of a name word, or within a small edit distance of one (typos, a swapped pair of letters).
/// </summary>
internal sealed class CatalogSearchIndex(IServiceScopeFactory scopes, IRouteTable routes, Shop.Application.Catalog.IMediaStorage media) : ICatalogSearch
{
    private const int MaxTokens = 5;

    private sealed record Entry(int Id, string Slug, string Name, string Key, string[] Words, long Price, string? ImagePath);

    private sealed record Snapshot(Entry[] Products, Entry[] Categories);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private Snapshot? _snapshot;
    private int _version;
    private int _builtVersion = -1;

    public void MarkChanged() => Interlocked.Increment(ref _version);

    private async Task<Snapshot> GetAsync(CancellationToken ct)
    {
        var wanted = Volatile.Read(ref _version);
        if (_snapshot is { } fresh && _builtVersion == wanted)
        {
            return fresh;
        }

        await _gate.WaitAsync(ct);
        try
        {
            wanted = Volatile.Read(ref _version);
            if (_snapshot is { } again && _builtVersion == wanted)
            {
                return again;
            }

            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
            var products = await db.Products.AsNoTracking()
                .Where(p => p.Status == ProductStatus.Published && p.Variants.Any(v => v.IsActive))
                .Select(p => new
                {
                    p.Id, p.Slug, p.Name, p.SearchKey,
                    Price = p.Variants.Where(v => v.IsActive).Min(v => v.Price),
                    Image = p.Images.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).Select(i => i.Path).FirstOrDefault(),
                }).ToListAsync(ct);
            var categories = await db.Categories.AsNoTracking().Where(c => c.IsActive).Select(c => new { c.Id, c.Slug, c.Name }).ToListAsync(ct);

            var snapshot = new Snapshot(
                products.Select(p => new Entry(p.Id, p.Slug, p.Name, p.SearchKey, Words(p.SearchKey), p.Price, p.Image)).ToArray(),
                categories.Select(c => new Entry(c.Id, c.Slug, c.Name, PersianText.SearchKey(c.Name), Words(PersianText.SearchKey(c.Name)), 0, null)).ToArray());
            _snapshot = snapshot;
            _builtVersion = wanted; // a change during the build leaves the version ahead, so the next query rebuilds again
            return snapshot;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string[] Words(string key) => key.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static string[] Tokens(string? query) =>
        PersianText.SearchKey(query).Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(MaxTokens).ToArray();

    /// <summary>Null when some word of the query matches nothing; otherwise a score (higher is better) and whether any word needed typo tolerance.</summary>
    private static (double Score, bool Fuzzy)? Match(string[] tokens, Entry entry)
    {
        double score = 0;
        var fuzzy = false;
        foreach (var token in tokens)
        {
            if (entry.Key.Contains(token, StringComparison.Ordinal))
            {
                score += entry.Words.Any(w => w.StartsWith(token, StringComparison.Ordinal)) ? 3.0 : 2.5;
                continue;
            }

            var allowed = EditDistance.Allowed(token.Length);
            var best = int.MaxValue;
            foreach (var word in entry.Words)
            {
                // The word itself, or its beginning of the same length (a half-typed word with a typo in it).
                best = Math.Min(best, EditDistance.Osa(token, word, allowed));
                if (word.Length > token.Length)
                {
                    best = Math.Min(best, EditDistance.Osa(token, word[..token.Length], allowed));
                }
            }

            if (best > allowed)
            {
                return null;
            }

            fuzzy = true;
            score += 2.0 - 0.5 * best;
        }

        return (score + (entry.Name.Length == 0 ? 0 : 1.0 / (1 + entry.Name.Length)), fuzzy);
    }

    private static List<(Entry Entry, double Score, bool Fuzzy)> Rank(string[] tokens, Entry[] entries) =>
        entries.Select(e => (Entry: e, Match: Match(tokens, e))).Where(x => x.Match is not null)
            .Select(x => (x.Entry, x.Match!.Value.Score, x.Match.Value.Fuzzy))
            .OrderByDescending(x => x.Score).ThenBy(x => x.Entry.Name, StringComparer.Ordinal).ToList();

    public async Task<SuggestDto> SuggestAsync(string? query, CancellationToken ct)
    {
        var tokens = Tokens(query);
        if (tokens.Length == 0)
        {
            return new SuggestDto([], [], false);
        }

        var snapshot = await GetAsync(ct);
        var products = Rank(tokens, snapshot.Products).Take(6).ToList();
        var categories = Rank(tokens, snapshot.Categories).Take(4).ToList();
        return new SuggestDto(
            products.Select(x => new SuggestProductDto(x.Entry.Slug, x.Entry.Name, routes.PathOf(RouteTargetTypes.Product, x.Entry.Id, x.Entry.Slug), x.Entry.Price, x.Entry.ImagePath is null ? null : media.GetUrl(x.Entry.ImagePath))).ToList(),
            categories.Select(x => new SuggestCategoryDto(x.Entry.Slug, x.Entry.Name, routes.PathOf(RouteTargetTypes.Category, x.Entry.Id, x.Entry.Slug))).ToList(),
            (products.Count > 0 && products.All(x => x.Fuzzy)) || (products.Count == 0 && categories.Count > 0 && categories.All(x => x.Fuzzy)));
    }

    public async Task<IReadOnlyList<int>> FuzzyProductIdsAsync(string? query, int limit, CancellationToken ct)
    {
        var tokens = Tokens(query);
        if (tokens.Length == 0)
        {
            return [];
        }

        return Rank(tokens, (await GetAsync(ct)).Products).Take(limit).Select(x => x.Entry.Id).ToList();
    }
}
