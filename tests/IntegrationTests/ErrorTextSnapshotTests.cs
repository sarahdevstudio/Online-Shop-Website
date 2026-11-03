using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Shop.IntegrationTests;

/// <summary>
/// Golden file of the user-facing text of many error answers (validation messages, problem messages, field labels). The texts
/// live in the translation table; this proves moving them there changes nothing a visitor reads until someone edits a text.
/// UPDATE_SNAPSHOTS=1 rewrites it after an intended change.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed record SettingsEnvelope(System.Text.Json.JsonElement Settings, byte[] RowVersion);

public sealed class ErrorTextSnapshotTests(ApiFixture fixture) : StoreTestBase(fixture)
{
    private static async Task<string> DescribeAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        var status = (int)response.StatusCode;
        if (text.Length == 0)
        {
            return $"{status} (empty)";
        }

        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        var builder = new StringBuilder($"{status} code={Str(root, "errorCode")} message={Str(root, "errorMessage")}");
        if (root.TryGetProperty("validationErrors", out var errors) && errors.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in errors.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                builder.Append("\n    ").Append(property.Name).Append(": ").Append(string.Join(" / ", property.Value.EnumerateArray().Select(v => v.GetString())));
            }
        }

        return builder.ToString();
    }

    private static string? Str(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    [Fact]
    public async Task ErrorTexts_AreUnchanged()
    {
        var admin = await LoginAdminAsync();
        var anon = NewClient();
        var shopper = NewShopper();
        var lines = new List<string>();

        async Task Admin(string name, HttpMethod method, string url, object? body = null) => lines.Add($"{name}: {await DescribeAsync(await SendAsync(admin, method, url, body))}");
        async Task Anon(string name, string url, object? body) => lines.Add($"{name}: {await DescribeAsync(await PostAsync(anon, url, body))}");
        async Task Get(string name, HttpClient client, string url) => lines.Add($"{name}: {await DescribeAsync(await client.GetAsync(url))}");
        async Task Store(string name, HttpMethod method, string url, object? body) => lines.Add($"{name}: {await DescribeAsync(await shopper.SendAsync(method, url, body, idempotencyKey: Guid.Parse("11111111-1111-1111-1111-111111111111")))}");

        // framework / generic
        await Get("not-found-route", anon, "/api/v1/nope");
        await Get("admin-anonymous", anon, "/api/v1/admin/ping");
        await Admin("admin-empty-category", HttpMethod.Post, "/api/v1/admin/categories", new { });
        await Admin("admin-bad-category-slug", HttpMethod.Post, "/api/v1/admin/categories", new { name = "x", slug = "Bad Slug!", sortOrder = 0 });
        await Admin("admin-category-unknown-parent", HttpMethod.Post, "/api/v1/admin/categories", new { name = "x", slug = Unique("zz"), parentId = 999999, sortOrder = 0 });
        await Admin("admin-category-404", HttpMethod.Put, "/api/v1/admin/categories/999999", new { name = "x", slug = "x", sortOrder = 0 });
        await Admin("admin-empty-brand", HttpMethod.Post, "/api/v1/admin/brands", new { });
        await Admin("admin-brand-404", HttpMethod.Delete, "/api/v1/admin/brands/999999");
        await Admin("admin-empty-product", HttpMethod.Post, "/api/v1/admin/products", new { });
        await Admin("admin-product-no-variants", HttpMethod.Post, "/api/v1/admin/products", new { name = "x", slug = Unique("p"), categoryId = 1, variants = Array.Empty<object>() });
        await Admin("admin-product-bad-variant", HttpMethod.Post, "/api/v1/admin/products", new { name = "x", slug = Unique("p"), categoryId = 999999, variants = new[] { new { sku = "bad sku!", price = 0, compareAtPrice = 5, stock = -1, sortOrder = 0, isActive = true } } });
        await Admin("admin-variant-404", HttpMethod.Put, "/api/v1/admin/variants/999999", new { sku = "a", price = 1, stock = 1, sortOrder = 0, isActive = true, rowVersion = "AAAA" });
        await Admin("admin-stock-adjust-bad", HttpMethod.Post, "/api/v1/admin/variants/999999/stock-adjust", new { delta = 0, reason = "x" });
        await Admin("admin-order-404", HttpMethod.Get, "/api/v1/admin/orders/999999");
        await Admin("admin-order-bad-filter", HttpMethod.Get, "/api/v1/admin/orders?status=nonsense");
        await Admin("admin-product-bad-status", HttpMethod.Get, "/api/v1/admin/products?status=bogus");
        await Admin("admin-blog-empty", HttpMethod.Post, "/api/v1/admin/blog", new { });
        await Admin("admin-blog-bad", HttpMethod.Post, "/api/v1/admin/blog", new { title = "x", slug = "Bad Slug", excerpt = "", body = "" });
        await Admin("admin-blog-404", HttpMethod.Get, "/api/v1/admin/blog/999999");
        await Admin("admin-content-404", HttpMethod.Get, "/api/v1/admin/content-pages/nope");
        await Admin("admin-content-bad", HttpMethod.Put, "/api/v1/admin/content-pages/about", new { title = "x", body = "", rowVersion = "AAAA" });
        var settings = await GetOkAsync<SettingsEnvelope>(admin, "/api/v1/admin/settings");
        await Admin("admin-settings-bad", HttpMethod.Put, "/api/v1/admin/settings", new
        {
            settings = new { shippingFlatFee = -1, freeShippingThreshold = -5, cardToCardEnabled = true, cashOnDeliveryEnabled = false, cardNumber = "1234", cardHolderName = new string('x', 300), bankName = new string('y', 300), maxQuantityPerLine = 0, storePhone = new string('1', 100), storeAddress = new string('a', 600), workingHours = new string('h', 300), instagramHandle = "bad handle!" },
            rowVersion = settings.RowVersion,
            currentPassword = "wrong-password-1",
        });
        await Admin("admin-settings-wrong-password", HttpMethod.Put, "/api/v1/admin/settings", new { settings = settings.Settings, rowVersion = settings.RowVersion, currentPassword = "wrong-password-1" });
        await Admin("admin-site-bad-kind", HttpMethod.Get, "/api/v1/admin/site/nope");
        await Admin("admin-site-seo-invalid", HttpMethod.Put, "/api/v1/admin/site/seo/draft", new { payload = new { siteName = "", titleTemplate = "no placeholder", homeTitle = "", defaultDescription = "", defaultOgMediaId = (int?)null }, rowVersion = (string?)null });
        await Admin("admin-site-badges-invalid", HttpMethod.Put, "/api/v1/admin/site/badges/draft", new { payload = new { enamad = new { id = "abc", code = "x" }, samandehi = new { id = "1", verifyCode = "x", logoCode = "y" } }, rowVersion = (string?)null });
        await Admin("admin-site-theme-invalid", HttpMethod.Put, "/api/v1/admin/site/theme/draft", new { payload = new { primary = "red", secondary = "#76007c", accent = "#d8c831", background = "#ffffff", surface = "#f2f2f2", text = "#1a1a1a", success = "#1b7f3b", warning = "#8a5a00", danger = "#b3261e", font = "comic", radius = "huge", logo = (object?)null, favicon = (object?)null }, rowVersion = (string?)null });
        await Admin("admin-site-chrome-invalid", HttpMethod.Put, "/api/v1/admin/site/chrome/draft", new { payload = new { announcement = new { enabled = true, text = "", link = "x y", style = "zzz", dismissible = true }, headerMenu = new[] { new { label = "", link = "javascript:x", allCategories = false, children = Array.Empty<object>() } }, footer = new { about = "", columns = Array.Empty<object>() }, social = new { telegram = "not a url !!" } }, rowVersion = (string?)null });
        await Admin("admin-site-home-invalid", HttpMethod.Put, "/api/v1/admin/site/home/draft", new { payload = new { blocks = new object[] { new { id = Guid.NewGuid(), type = "hero", enabled = true, data = new { autoplaySeconds = 3, slides = Array.Empty<object>() } }, new { id = Guid.NewGuid(), type = "productRow", enabled = true, data = new { title = "", source = "category", categoryId = 999999, productIds = Array.Empty<int>(), limit = 2 } } } }, rowVersion = (string?)null });
        await Admin("admin-site-discard-none", HttpMethod.Post, "/api/v1/admin/site/seo/discard", new { rowVersion = "AAAA" });

        // auth
        await Anon("login-empty", "/api/v1/auth/login", new { });
        await Anon("login-wrong", "/api/v1/auth/login", new { username = "nobody", password = "wrong-password-1" });
        await Anon("register-empty", "/api/v1/auth/register", new { });
        await Anon("register-weak", "/api/v1/auth/register", new { email = "a@b.co", password = "abc", fullName = "x", phone = "1" });
        lines.Add($"change-password-weak: {await DescribeAsync(await SendAsync(admin, HttpMethod.Post, "/api/v1/auth/change-password", new { currentPassword = Fixture.AdminPassword, newPassword = "short" }))}");
        lines.Add($"change-password-wrong: {await DescribeAsync(await SendAsync(admin, HttpMethod.Post, "/api/v1/auth/change-password", new { currentPassword = "wrong-password-1", newPassword = "Another-Strong-Pass-1!" }))}");

        // public catalog parsing
        await Get("list-bad-sort", anon, "/api/v1/catalog/products?sort=nonsense");
        await Get("list-bad-bool", anon, "/api/v1/catalog/products?inStock=maybe");
        await Get("list-bad-price-range", anon, "/api/v1/catalog/products?minPrice=500&maxPrice=100");
        await Get("list-long-q", anon, "/api/v1/catalog/products?q=" + new string('a', 300));
        await Get("category-404", anon, "/api/v1/catalog/categories/nope");
        await Get("product-404", anon, "/api/v1/catalog/products/nope");

        // storefront
        await Store("cart-add-empty", HttpMethod.Post, "/api/v1/store/cart/items", new { });
        await Store("cart-add-unknown", HttpMethod.Post, "/api/v1/store/cart/items", new { variantId = 999999, quantity = 1 });
        await Store("order-empty", HttpMethod.Post, "/api/v1/store/orders", new { });
        await Store("order-empty-cart", HttpMethod.Post, "/api/v1/store/orders", new { fullName = "علی رضایی", mobile = "09123456789", province = "تهران", city = "تهران", address = "خیابان آزادی پلاک ۱۰ واحد ۲", postalCode = "1234567890", paymentMethod = "cardToCard", expectedTotal = 1 });
        await Store("order-bad-fields", HttpMethod.Post, "/api/v1/store/orders", new { fullName = "", mobile = "12", province = "", city = "", address = "کوتاه", postalCode = "12", paymentMethod = "", expectedTotal = 1 });
        await Store("order-lookup-404", HttpMethod.Get, "/api/v1/store/orders/" + new string('A', 43), null);
        await Store("transfer-ref-bad", HttpMethod.Post, "/api/v1/store/orders/" + new string('A', 43) + "/transfer-reference", new { });

        Snapshot.Assert("error-texts.txt", string.Join('\n', lines) + '\n');
    }
}
