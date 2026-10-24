using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Shop.Application.Orders;

namespace Shop.Api.Infrastructure;

public sealed class StorefrontOptions
{
    public const string SectionName = "Storefront";

    /// <summary>Shared secret of the Next.js server; required by every <c>/api/v1/store</c> request.</summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Addresses (or CIDR networks) of the storefront server(s) allowed to supply the client IP in X-Forwarded-For.
    /// Empty = the framework default (loopback only). A forwarded address from any other peer is ignored.
    /// </summary>
    public string[] TrustedProxies { get; set; } = [];
}

/// <summary>Marks the endpoints that are called server-to-server by the storefront with no ambient browser credentials.</summary>
internal sealed class StorefrontEndpointMetadata
{
    public static readonly StorefrontEndpointMetadata Instance = new();
}

/// <summary>
/// The <c>/api/v1/store</c> endpoints use no cookie authentication (identity is the explicit cart / order token), so
/// there is nothing for CSRF to ride on; instead only the Next.js server, which knows the shared key, may call them.
/// </summary>
internal sealed class StorefrontKeyFilter(IOptions<StorefrontOptions> options) : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var supplied = context.HttpContext.Request.Headers[StorefrontKeyRules.HeaderName].ToString();
        if (!StorefrontKeyRules.Matches(options.Value.ApiKey, supplied))
        {
            return ValueTask.FromResult<object?>(Results.Problem(
                "A valid storefront key is required.",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                extensions: new Dictionary<string, object?> { ["code"] = "storefront_key_invalid" }));
        }

        return next(context);
    }
}

internal static class ClientIp
{
    /// <summary>
    /// The caller's address as ASP.NET resolved it: the socket address, replaced by X-Forwarded-For only when the socket
    /// peer is a configured trusted proxy (see <c>UseForwardedHeaders</c>).
    /// </summary>
    public static string From(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address is null)
        {
            return "unknown";
        }

        return (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
    }
}

/// <summary>Order access tokens and cart tokens must never reach logs.</summary>
public static partial class LogPaths
{
    public static string Mask(string? path) =>
        string.IsNullOrEmpty(path) ? string.Empty : TokenSegment().Replace(path, "$1/***");

    public static string Mask(PathString path) => Mask(path.Value);

    // /api/v1/store/orders/{43-char token}[/...]  and the storefront's /order/{token}
    [GeneratedRegex("(/orders?)/[A-Za-z0-9_-]{43}(?![A-Za-z0-9_-])", RegexOptions.CultureInvariant)]
    private static partial Regex TokenSegment();
}
