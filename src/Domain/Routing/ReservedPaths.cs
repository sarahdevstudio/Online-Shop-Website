namespace Shop.Domain.Routing;

/// <summary>
/// The ONE list of paths the storefront serves itself, which an admin-chosen path can therefore never take. A path is reserved
/// when its first segment is in <see cref="Segments"/> (so <c>/cart</c> and <c>/order/abc</c> are both refused) or when it is
/// exactly one of <see cref="Exact"/> (the blog index: <c>/blog</c> is reserved but <c>/blog/my-post</c> is where posts live).
/// The test <c>ReservedPathsTests</c> reads the storefront's <c>app/</c> and <c>public/</c> folders and fails when a static
/// route exists that is not listed here: adding a static route means adding it here, in the same commit.
/// </summary>
public static class ReservedPaths
{
    /// <summary>First segments owned by the storefront, the API proxy, the framework or the browser.</summary>
    public static IReadOnlyList<string> Segments { get; } =
    [
        "_next",          // framework assets
        "account",        // customer area (static page)
        "admin",          // admin panel
        "api",            // storefront route handlers
        "cart",
        "checkout",
        "favicon.ico",
        "health",
        "media",          // uploaded images pass-through
        "order",          // /order/{token}
        "robots.txt",
        "search",
        "site-icon",      // favicon / touch icon route
        "sitemap.xml",
    ];

    /// <summary>Whole paths (not prefixes) owned by static pages.</summary>
    public static IReadOnlyList<string> Exact { get; } = ["/blog"];

    /// <summary><paramref name="normalizedPath"/> must already be normalized (see <see cref="RoutePath.Normalize"/>).</summary>
    public static bool IsReserved(string normalizedPath)
    {
        if (Exact.Contains(normalizedPath, StringComparer.Ordinal))
        {
            return true;
        }

        var end = normalizedPath.IndexOf('/', 1);
        var first = end < 0 ? normalizedPath[1..] : normalizedPath[1..end];
        return Segments.Contains(first, StringComparer.Ordinal);
    }
}
