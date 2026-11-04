namespace Shop.Domain.Routing;

/// <summary>What a public path can point to. The strings are part of the API (resolve response, admin endpoints).</summary>
public static class RouteTargetTypes
{
    public const string Category = "category";
    public const string Product = "product";
    public const string ContentPage = "contentPage";
    public const string BlogPost = "blogPost";

    public static IReadOnlyList<string> All { get; } = [Category, Product, ContentPage, BlogPost];

    public static bool IsKnown(string? type) => type is not null && All.Contains(type, StringComparer.Ordinal);

    /// <summary>
    /// The path an entity has when nobody chose one: exactly the URL it always had (docs/11 C.1 #3), so switching dynamic
    /// routing on moves nothing. Info pages live at the root (<c>/about</c>).
    /// </summary>
    public static string DefaultPath(string type, string slug) => type switch
    {
        Category => $"/category/{slug}",
        Product => $"/product/{slug}",
        BlogPost => $"/blog/{slug}",
        ContentPage => $"/{slug}",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown route target type."),
    };
}
