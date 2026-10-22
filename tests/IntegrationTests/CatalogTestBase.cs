using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shop.IntegrationTests;

public sealed record IdBody(int Id);

public sealed record AdminOptionBody(int Id, string Value, string Slug);

public sealed record AdminAttributeBody(int Id, string Code, string Type, AdminOptionBody[] Options);

public sealed record AdminVariantBody(int Id, string Sku, long Price, long? CompareAtPrice, int Stock, bool IsActive, int SortOrder, byte[] RowVersion);

public sealed record AdminImageBody(int Id, string Url, string Alt, int SortOrder);

public sealed record AdminProductBody(int Id, string Slug, string Name, string Status, AdminVariantBody[] Variants, AdminImageBody[] Images);

public sealed record CardBody(string Slug, string Name, string? Brand, long Price, long? CompareAtPrice, bool InStock);

public sealed record FacetOptionBody(string Slug, string Value, int Count);

public sealed record AttributeFacetBody(string Code, FacetOptionBody[] Options);

public sealed record PriceRangeBody(long Min, long Max);

public sealed record FacetsBody(FacetOptionBody[] Brands, AttributeFacetBody[] Attributes, PriceRangeBody? Price);

public sealed record ProductListBody(CardBody[] Items, int Page, int PageSize, int Total, FacetsBody Facets);

public sealed record VariantBody(int Id, string Sku, long Price, bool InStock, Dictionary<string, string> Attributes);

public sealed record AxisBody(string Code, AdminOptionBody[] Options);

public sealed record SpecBody(string Code, string Value);

public sealed record ProductDetailBody(string Slug, string Name, string Status, AxisBody[] Axes, VariantBody[] Variants, SpecBody[] Specs);

public sealed record SitemapEntryBody(string Slug);

public sealed record SitemapBody(SitemapEntryBody[] Categories, SitemapEntryBody[] Products);

/// <summary>Builds catalog data through the real admin API. Every test uses fresh slugs, so tests never see each other's data.</summary>
public abstract class CatalogTestBase(ApiFixture fixture) : ApiTestBase(fixture)
{
    protected static readonly JsonSerializerOptions ApiJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    protected static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..Math.Min(prefix.Length + 13, 60)];

    protected static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string url, object? body = null)
    {
        var csrf = await client.GetFromJsonAsync<CsrfBody>("/api/v1/auth/csrf", Json);
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: ApiJson);
        }

        request.Headers.Add(csrf!.HeaderName, csrf.Token);
        return await client.SendAsync(request);
    }

    protected static async Task<T> SendOkAsync<T>(HttpClient client, HttpMethod method, string url, object? body = null)
    {
        var response = await SendAsync(client, method, url, body);
        Assert.True(response.IsSuccessStatusCode, $"{method} {url} → {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(ApiJson))!;
    }

    protected static async Task<T> GetOkAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"GET {url} → {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<T>(ApiJson))!;
    }

    protected static Task<IdBody> CreateCategoryAsync(HttpClient admin, string slug, int? parentId = null, bool isActive = true) =>
        SendOkAsync<IdBody>(admin, HttpMethod.Post, "/api/v1/admin/categories",
            new { name = "دسته " + slug, slug, parentId, sortOrder = 0, isActive });

    protected static Task<IdBody> CreateBrandAsync(HttpClient admin, string slug) =>
        SendOkAsync<IdBody>(admin, HttpMethod.Post, "/api/v1/admin/brands", new { name = "برند " + slug, slug, isActive = true });

    protected static Task<AdminProductBody> CreateProductAsync(
        HttpClient admin, int categoryId, string slug, int? brandId = null, string status = "published", string? name = null,
        params (long Price, int Stock)[] variants) =>
        SendOkAsync<AdminProductBody>(admin, HttpMethod.Post, "/api/v1/admin/products", new
        {
            categoryId,
            brandId,
            name = name ?? "محصول " + slug,
            slug,
            description = "توضیح",
            status,
            variants = (variants.Length == 0 ? new[] { (Price: 100_000L, Stock: 5) } : variants)
                .Select((v, i) => new { sku = $"{slug}-{i}"[..Math.Min(slug.Length + 2, 60)], price = v.Price, compareAtPrice = (long?)null, stock = v.Stock, isActive = true, sortOrder = i }),
        });

    protected async Task<Dictionary<string, AdminAttributeBody>> AttributesAsync(HttpClient admin) =>
        (await GetOkAsync<AdminAttributeBody[]>(admin, "/api/v1/admin/attributes")).ToDictionary(a => a.Code);

    /// <summary>Sets option values: product-level when <paramref name="variantId"/> is null.</summary>
    protected static object Opt(Dictionary<string, AdminAttributeBody> attributes, string code, string slug, int? variantId = null) =>
        new { attributeId = attributes[code].Id, variantId, optionId = attributes[code].Options.Single(o => o.Slug == slug).Id, text = (string?)null };

    protected static Task<AdminProductBody> SetAttributesAsync(HttpClient admin, int productId, params object[] values) =>
        SendOkAsync<AdminProductBody>(admin, HttpMethod.Put, $"/api/v1/admin/products/{productId}/attributes", new { values });
}
