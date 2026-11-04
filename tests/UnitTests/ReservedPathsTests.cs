using Shop.Domain.Routing;

namespace Shop.UnitTests;

/// <summary>
/// The reserved-path list (<see cref="ReservedPaths"/>) must cover every static route the storefront serves, now and after the
/// next commit: this reads the storefront's <c>src/app</c> and <c>public</c> folders and fails when a route exists that an
/// admin-chosen path could shadow or be shadowed by. Adding a static route means adding it to the list, in the same commit.
/// </summary>
public class ReservedPathsTests
{
    private static string Storefront()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "storefront", "src", "app")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("Repository root not found."), "storefront");
    }

    private static readonly string[] RouteFiles = ["page", "route"];

    private sealed record StaticRoute(string Path, bool HasDynamicChild, string Source);

    /// <summary>
    /// Every URL the app folder serves: route groups "(x)" and private folders "_x" don't count, a dynamic segment ends the
    /// static prefix. A top-level dynamic segment (the dynamic-routing catch-all) is the thing the list protects, not a route.
    /// </summary>
    private static List<StaticRoute> DiscoverRoutes()
    {
        var app = Path.Combine(Storefront(), "src", "app");
        var routes = new List<StaticRoute>();
        foreach (var file in Directory.EnumerateFiles(app, "*.*", SearchOption.AllDirectories))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            var relative = Path.GetRelativePath(app, Path.GetDirectoryName(file)!).Replace('\\', '/');
            var segments = relative == "." ? [] : relative.Split('/').Where(s => !(s.StartsWith('(') && s.EndsWith(')'))).ToArray();
            if (segments.Any(s => s.StartsWith('_') || s.StartsWith('@')))
            {
                continue;
            }

            var isRoute = RouteFiles.Contains(name);
            // Metadata routes at the app root: robots.ts → /robots.txt, sitemap.ts → /sitemap.xml.
            var metadata = segments.Length == 0 ? name switch { "robots" => "robots.txt", "sitemap" => "sitemap.xml", "favicon" => "favicon.ico", _ => null } : null;
            if (!isRoute && metadata is null)
            {
                continue;
            }

            var staticPart = segments.TakeWhile(s => !s.StartsWith('[')).ToList();
            var dynamic = segments.Length > staticPart.Count;
            if (metadata is not null)
            {
                staticPart.Add(metadata);
            }

            if (staticPart.Count == 0)
            {
                continue; // the home page "/" and a top-level catch-all
            }

            routes.Add(new StaticRoute("/" + string.Join('/', staticPart), dynamic, Path.GetRelativePath(Storefront(), file).Replace('\\', '/')));
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(Path.Combine(Storefront(), "public")))
        {
            routes.Add(new StaticRoute("/" + Path.GetFileName(entry), Directory.Exists(entry), "public/" + Path.GetFileName(entry)));
        }

        return routes;
    }

    [Fact]
    public void EveryStaticRoute_OfTheStorefront_IsReserved()
    {
        var routes = DiscoverRoutes();
        Assert.True(routes.Count > 10, "The storefront routes were not found; the discovery is broken.");

        var unreserved = routes
            .Where(r => r.HasDynamicChild ? !ReservedPaths.IsReserved(r.Path + "/x") : !ReservedPaths.IsReserved(r.Path))
            .Select(r => $"{r.Path}{(r.HasDynamicChild ? "/…" : "")}  ({r.Source})")
            .Distinct()
            .ToList();

        Assert.True(
            unreserved.Count == 0,
            "These storefront routes are not in Shop.Domain.Routing.ReservedPaths, so an admin could choose a path that collides with them:\n"
            + string.Join('\n', unreserved));
    }

    [Fact]
    public void TheDiscovery_FindsTheRoutesWeKnowAbout()
    {
        var paths = DiscoverRoutes().Select(r => r.Path).ToHashSet();
        Assert.All(
            new[] { "/cart", "/checkout", "/search", "/order", "/admin", "/api", "/media", "/blog", "/robots.txt", "/sitemap.xml", "/site-icon", "/fonts" },
            expected => Assert.Contains(paths, p => p == expected || p.StartsWith(expected + "/", StringComparison.Ordinal)));
    }

    [Fact]
    public void TheListItself_HasNoDuplicatesAndIsLowerCase()
    {
        Assert.Equal(ReservedPaths.Segments.Count, ReservedPaths.Segments.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ReservedPaths.Segments.Concat(ReservedPaths.Exact), p => Assert.Equal(p, p.ToLowerInvariant()));
        Assert.All(ReservedPaths.Exact, p => Assert.StartsWith("/", p));
    }
}
