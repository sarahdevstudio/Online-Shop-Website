using Shop.Application.Catalog;
using Shop.Application.Common;

namespace Shop.Application.Routing;

/// <summary>
/// What a public path points to (<c>GET /api/v1/catalog/resolve</c>). Always a 200 so the storefront can cache every answer:
/// <list type="bullet">
/// <item><c>Kind</c> = category | product | contentPage | blogPost: <c>Id</c>, <c>Slug</c> and the entity's current <c>Path</c> are set.</item>
/// <item><c>Kind</c> = redirect: <c>To</c> is the current path, <c>Status</c> is 308. Always ONE hop.</item>
/// <item><c>Kind</c> = gone: the entity was hard-deleted (410). <c>Kind</c> = notFound: nothing lives here (404).</item>
/// </list>
/// </summary>
public sealed record RouteResolutionDto(string Kind, int? Id = null, string? Slug = null, string? Path = null, string? To = null, int? Status = null)
{
    public const string Redirect = "redirect";
    public const string Gone = "gone";
    public const string NotFound = "notFound";

    public static RouteResolutionDto Missing { get; } = new(NotFound);
}

/// <summary>Request-path lookups answered from memory (no SQL per request): see <c>RouteTable</c>.</summary>
public interface IRouteTable
{
    RouteResolutionDto Resolve(string? requestPath);

    /// <summary>The entity's current path, or its default path (the URL it always had) when it has no row yet.</summary>
    string PathOf(string targetType, int targetId, string slug);

    /// <summary>The CURRENT path of an entity that still exists, or null when it was deleted (links to it are then hidden).</summary>
    string? CurrentPathOf(string targetType, int targetId);

    /// <summary>Changes whenever the table is rebuilt; caches built from paths compare it to know they are stale.</summary>
    int Version { get; }

    Task ReloadAsync(CancellationToken ct = default);
}

public sealed record ChangeRoutePathRequest(string Path, byte[]? RowVersion);

public sealed record OldRoutePathDto(int Id, string Path, DateTime CreatedAtUtc);

/// <summary>An entity's current path (with the version the form must send back) and the old paths that redirect to it.</summary>
public sealed record RouteTargetDto(string Type, int Id, string Path, byte[] RowVersion, IReadOnlyList<OldRoutePathDto> OldPaths);

/// <summary>One row of the redirects page: an old path (or the path of a deleted entity) and where it leads.</summary>
public sealed record RedirectDto(
    int Id, string Path, string TargetType, int TargetId, string TargetName, string? CurrentPath, bool IsGone, DateTime CreatedAtUtc);

public sealed record RouteAuditEntryDto(
    DateTime AtUtc, string TargetType, int TargetId, string TargetName, string Action, string? OldPath, string? NewPath, string? AdminUsername);

public interface IRouteService
{
    Task<Result<RouteTargetDto>> GetAsync(string type, int id, CancellationToken ct);

    Task<Result<RouteTargetDto>> ChangeAsync(string type, int id, ChangeRoutePathRequest request, CancellationToken ct);

    Task<PagedList<RedirectDto>> ListRedirectsAsync(string? q, int page, CancellationToken ct);

    Task<Result> ReleaseAsync(int routeId, CancellationToken ct);

    Task<PagedList<RouteAuditEntryDto>> AuditAsync(int page, CancellationToken ct);

    /// <summary>
    /// Before an entity is created: is <paramref name="path"/> (or, when null, the default path for <paramref name="slug"/>)
    /// valid, not reserved and free? Lets the create fail with a clear message instead of leaving an entity without its URL.
    /// </summary>
    Task<Result> CheckAvailableForNewAsync(string type, string slug, string? path, CancellationToken ct);

    /// <summary>
    /// Right after the entity row exists, inside the same transaction: gives it its first, current path. The caller commits and
    /// then calls <see cref="IRouteTable.ReloadAsync"/> (or uses <see cref="CompleteAsync"/>).
    /// </summary>
    Task<Result> AddForNewAsync(string type, int id, string slug, string? path, CancellationToken ct);

    /// <summary>A hard-deleted entity: all its paths answer 410 from now on. Inside the deleting transaction.</summary>
    Task MarkGoneAsync(string type, int id, CancellationToken ct);

    /// <summary>After the surrounding transaction committed: rebuild the in-memory table.</summary>
    Task CompleteAsync(CancellationToken ct);
}
