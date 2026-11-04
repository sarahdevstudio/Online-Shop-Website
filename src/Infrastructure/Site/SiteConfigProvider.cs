using Microsoft.Extensions.DependencyInjection;
using Shop.Application.Routing;
using Shop.Application.Site;

namespace Shop.Infrastructure.Site;

/// <summary>
/// The compiled published document, cached in memory. Publish and restore call <see cref="Invalidate"/>; the age limit is only
/// a safety net for a manual database edit (single API instance, like the store settings).
/// </summary>
internal sealed class SiteConfigProvider(IServiceScopeFactory scopes, TimeProvider clock, IRouteTable routes) : ISiteConfigService
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(10);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private Cached? _cached;
    private int _generation;

    public async Task<SiteConfigDto> GetPublishedAsync(CancellationToken ct)
    {
        if (Fresh() is { } hit)
        {
            return hit;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (Fresh() is { } again)
            {
                return again;
            }

            var generationBefore = Volatile.Read(ref _generation);
            var routesBefore = routes.Version;
            await using var scope = scopes.CreateAsyncScope();
            var document = await scope.ServiceProvider.GetRequiredService<SiteCompiler>().CompileAsync(useDrafts: false, ct);

            // A publish that happened while we were compiling must not be overwritten by what we read before it.
            if (generationBefore == Volatile.Read(ref _generation))
            {
                _cached = new Cached(document, clock.GetUtcNow(), routesBefore);
            }

            return document;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Invalidate()
    {
        Interlocked.Increment(ref _generation);
        _cached = null;
    }

    /// <summary>Stale after <see cref="MaxAge"/>, after a publish, and whenever the route table was rebuilt (a path changed, an entity was deleted).</summary>
    private SiteConfigDto? Fresh() =>
        _cached is { } cached && clock.GetUtcNow() - cached.At < MaxAge && cached.RouteVersion == routes.Version ? cached.Document : null;

    private sealed record Cached(SiteConfigDto Document, DateTimeOffset At, int RouteVersion);
}
