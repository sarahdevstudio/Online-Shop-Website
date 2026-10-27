using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace Shop.IntegrationTests;

/// <summary>
/// The contract of the HTTP API as golden files (tests/IntegrationTests/Snapshots). They were captured from the
/// minimal-API implementation and must not change while endpoints are moved to controllers:
/// <list type="bullet">
/// <item><c>routes.txt</c>: every /api/v1 endpoint with its verbs, template, authorization, rate limit and storefront marker.</item>
/// <item><c>behavior.txt</c>: what every endpoint answers (status, problem code and shape) anonymously, with the storefront key,
/// with a CSRF token only, and as the signed-in admin; this proves the CSRF, key, validation and binding filters.</item>
/// <item><c>openapi.json</c>: the OpenAPI document, normalized.</item>
/// </list>
/// Set UPDATE_SNAPSHOTS=1 to rewrite them after an intended API change.
/// </summary>
public sealed class ApiContractTests(ApiFixture fixture) : CatalogTestBase(fixture)
{
    /// <summary>The route template without the differences that are only about how it was declared (leading and trailing slash).</summary>
    private static string Template(RouteEndpoint endpoint) => "/" + (endpoint.RoutePattern.RawText ?? "").Trim('/');

    private static readonly Regex Placeholder = new(@"\{(?<name>\w+)(:[^}]*)?\}", RegexOptions.Compiled);

    [Fact]
    public void RouteTable_IsUnchanged()
    {
        var endpoints = Fixture.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();
        var lines = new List<string>();
        foreach (var endpoint in endpoints.Where(e => Template(e).StartsWith("/api/v1", StringComparison.Ordinal)))
        {
            var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];
            var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()
                .Select(a => a.Policy is { Length: > 0 } policy ? policy : a.Roles is { Length: > 0 } ? "roles:" + a.Roles : "authenticated")
                .Distinct().Order(StringComparer.Ordinal);
            var anonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null ? "anonymous" : "-";
            var limiter = endpoint.Metadata.GetOrderedMetadata<EnableRateLimitingAttribute>().Select(a => a.PolicyName ?? "").FirstOrDefault() ?? "-";
            var storefront = endpoint.Metadata.Any(m => m.GetType().Name.StartsWith("StorefrontEndpoint", StringComparison.Ordinal)) ? "storefront" : "-";
            lines.Add($"{string.Join(",", methods.Order(StringComparer.Ordinal))} {Template(endpoint)} | auth: {string.Join("+", policies)} | {anonymous} | rate: {limiter} | {storefront}");
        }

        Snapshot.Assert("routes.txt", string.Join('\n', lines.Order(StringComparer.Ordinal)) + '\n');
    }

    [Fact]
    public async Task EndpointBehavior_IsUnchanged()
    {
        var endpoints = Fixture.Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => Template(e).StartsWith("/api/v1", StringComparison.Ordinal))
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []).Select(m => (Method: m, Pattern: Template(e))))
            .Distinct()
            .OrderBy(e => e.Pattern, StringComparer.Ordinal).ThenBy(e => e.Method, StringComparer.Ordinal)
            .ToList();

        var anonymous = NewClient();
        var csrfOnly = NewClient();
        var admin = await LoginAdminAsync();
        var csrfToken = await csrfOnly.GetFromJsonAsync<CsrfBody>("/api/v1/auth/csrf", Json);

        var lines = new List<string>();
        foreach (var (method, pattern) in endpoints)
        {
            var url = Placeholder.Replace(pattern, match => match.Groups["name"].Value switch
            {
                "id" or "variantId" => "999999",
                "token" => new string('a', 43),
                _ => "x",
            });
            var unsafeMethod = method is not ("GET" or "HEAD");
            var scenarios = new List<string>
            {
                "anon=" + await DescribeAsync(anonymous, method, url, unsafeMethod, csrf: null, key: false),
                "key=" + await DescribeAsync(anonymous, method, url, unsafeMethod, csrf: null, key: true),
                "csrf=" + await DescribeAsync(csrfOnly, method, url, unsafeMethod, csrf: csrfToken, key: false),
            };

            // Not run as the admin: they would sign the shared admin client out or change shared settings.
            var skipAdmin = pattern is "/api/v1/auth/logout" or "/api/v1/auth/change-password"
                || (pattern == "/api/v1/admin/settings" && method == "PUT");
            if (!skipAdmin)
            {
                var adminToken = await admin.GetFromJsonAsync<CsrfBody>("/api/v1/auth/csrf", Json);
                scenarios.Add("admin=" + await DescribeAsync(admin, method, url, unsafeMethod, csrf: adminToken, key: false));
            }

            lines.Add($"{method} {pattern} | {string.Join(" | ", scenarios)}");
        }

        Snapshot.Assert("behavior.txt", string.Join('\n', lines) + '\n');
    }

    [Fact]
    public async Task OpenApiDocument_IsUnchanged()
    {
        var response = await NewClient().GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        if (document["tags"] is JsonArray tags)
        {
            // The order of the top-level tag list follows the order endpoints were registered in, which carries no meaning.
            document["tags"] = new JsonArray(tags.OrderBy(t => t?["name"]?.GetValue<string>(), StringComparer.Ordinal).Select(t => t?.DeepClone()).ToArray());
        }

        var normalized = Canonical(document)!.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        Snapshot.Assert("openapi.json", normalized + '\n');
    }

    /// <summary>Same document with every object's keys sorted, so the file only changes when the API's description does.</summary>
    private static JsonNode? Canonical(JsonNode? node) => node switch
    {
        JsonObject obj => new JsonObject(obj.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => KeyValuePair.Create(p.Key, Canonical(p.Value)))),
        JsonArray array => new JsonArray(array.Select(Canonical).ToArray()),
        null => null,
        _ => node.DeepClone(),
    };

    private static async Task<string> DescribeAsync(HttpClient client, string method, string url, bool unsafeMethod, CsrfBody? csrf, bool key)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (unsafeMethod)
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        if (csrf is not null)
        {
            request.Headers.Add(csrf.HeaderName, csrf.Token);
        }

        if (key)
        {
            request.Headers.Add("X-Storefront-Key", ApiFixture.StorefrontKey);
        }

        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        var status = (int)response.StatusCode;
        if (response.Content.Headers.ContentType?.MediaType == "application/problem+json")
        {
            using var problem = JsonDocument.Parse(text);
            var root = problem.RootElement;
            var code = root.TryGetProperty("code", out var c) ? c.GetString() : null;
            var title = root.TryGetProperty("title", out var t) ? t.GetString() : null;
            var shape = string.Join(",", root.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
            var errors = root.TryGetProperty("errors", out var e) && e.ValueKind == JsonValueKind.Object
                ? "{" + string.Join(",", e.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)) + "}"
                : "";
            return $"{status} problem[{shape}] code={code} title={title} errors={errors}";
        }

        var kind = text.Length == 0 ? "empty" : response.Content.Headers.ContentType?.MediaType ?? "?";
        return $"{status} {kind}";
    }
}

/// <summary>Golden-file helper: compares text with tests/IntegrationTests/Snapshots/&lt;name&gt; (UPDATE_SNAPSHOTS=1 rewrites it).</summary>
internal static class Snapshot
{
    private static string Folder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Shop.IntegrationTests.csproj")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("Test project folder not found."), "Snapshots");
    }

    public static void Assert(string name, string actual)
    {
        var path = Path.Combine(Folder(), name);
        actual = actual.Replace("\r\n", "\n");
        if (Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "1" || !File.Exists(path))
        {
            Directory.CreateDirectory(Folder());
            File.WriteAllText(path, actual, new UTF8Encoding(false));
            if (!File.Exists(path))
            {
                throw new InvalidOperationException("Snapshot could not be written.");
            }

            return;
        }

        var expected = File.ReadAllText(path).Replace("\r\n", "\n");
        if (expected != actual)
        {
            var temp = Path.Combine(Path.GetTempPath(), "actual-" + name);
            File.WriteAllText(temp, actual, new UTF8Encoding(false));
            Xunit.Assert.Fail($"{name} changed. Compare with the golden file: diff \"{path}\" \"{temp}\" (UPDATE_SNAPSHOTS=1 accepts an intended change).");
        }
    }
}
