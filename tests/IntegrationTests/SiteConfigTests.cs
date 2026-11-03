using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shop.Domain.Site;
using Shop.Infrastructure.Persistence;

namespace Shop.IntegrationTests;

public sealed record SiteDraftBody(JsonElement Payload, byte[] RowVersion, DateTime UpdatedAtUtc, string? UpdatedBy);

public sealed record SitePublishedBody(int Version, JsonElement Payload, DateTime PublishedAtUtc, string? PublishedBy);

public sealed record SiteHistoryBody(int Version, string State, DateTime? PublishedAtUtc, string? PublishedBy);

public sealed record SiteMediaBody(string Url, int Width, int Height, string Purpose);

public sealed record SiteKindBody(
    string Kind, SitePublishedBody? Published, SiteDraftBody? Draft, SiteHistoryBody[] History,
    Dictionary<string, SiteMediaBody> Media, JsonElement? Report);

public sealed record SiteImageBody(int Id, string Url, int Width, int Height, long Bytes, string Purpose, string[] Warnings);

public sealed record SiteAuditEntryBody(DateTime AtUtc, string Kind, string Action, int? Version, string Summary, string? AdminUsername);

public sealed record SiteAuditPageBody(SiteAuditEntryBody[] Items, int Total, int Page, int PageCount);

public sealed class SiteConfigTests(ApiFixture fixture) : CatalogTestBase(fixture)
{
    // ---------------------------------------------------------------- helpers

    private static string Url(string kind, string tail = "") => $"/api/v1/admin/site/{kind}{tail}";

    private static Task<SiteKindBody> GetKindAsync(HttpClient admin, string kind) => GetOkAsync<SiteKindBody>(admin, Url(kind));

    /// <summary>Starts from "no draft" so tests do not depend on each other.</summary>
    private static async Task<SiteKindBody> CleanAsync(HttpClient admin, string kind)
    {
        var current = await GetKindAsync(admin, kind);
        if (current.Draft is { } draft)
        {
            return await SendOkAsync<SiteKindBody>(admin, HttpMethod.Post, Url(kind, "/discard"), new { rowVersion = draft.RowVersion });
        }

        return current;
    }

    private static Task<HttpResponseMessage> SaveAsync(HttpClient admin, string kind, object payload, byte[]? rowVersion = null) =>
        SendAsync(admin, HttpMethod.Put, Url(kind, "/draft"), new { payload, rowVersion });

    private static async Task<SiteKindBody> SaveOkAsync(HttpClient admin, string kind, object payload, byte[]? rowVersion = null)
    {
        var response = await SaveAsync(admin, kind, payload, rowVersion);
        Assert.True(response.IsSuccessStatusCode, $"save {kind} → {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<Envelope<SiteKindBody>>(ApiJson))!.Result!;
    }

    private static Task<SiteKindBody> PublishOkAsync(HttpClient admin, string kind, byte[] rowVersion) =>
        SendOkAsync<SiteKindBody>(admin, HttpMethod.Post, Url(kind, "/publish"), new { rowVersion });

    private static object Seo(string homeTitle = "عنوان صفحهٔ اصلی", int? og = null) => new
    {
        siteName = "قصر روشنایی",
        titleTemplate = "%s | قصر روشنایی",
        homeTitle,
        defaultDescription = "توضیح پیش‌فرض",
        defaultOgMediaId = og,
    };

    private static object Theme(string text = "#1a1a1a", string font = "vazirmatn") => new
    {
        primary = "#5b0057", secondary = "#76007c", accent = "#d8c831", background = "#ffffff", surface = "#f2f2f2", text,
        success = "#1b7f3b", warning = "#8a5a00", danger = "#b3261e", font, radius = "sm", logo = (object?)null, favicon = (object?)null,
    };

    private static object TextBlock(string heading, string body, bool enabled = true) => new
    {
        id = Guid.NewGuid(), type = "text", enabled, data = new { heading, body },
    };

    private static object Home(params object[] blocks) => new { blocks };

    /// <summary>All text of a JSON value (property names and strings, decoded), for "does the document mention X" checks.</summary>
    private static string Text(JsonElement element)
    {
        var builder = new System.Text.StringBuilder();
        void Walk(JsonElement e)
        {
            switch (e.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in e.EnumerateObject())
                    {
                        builder.Append(property.Name).Append(' ');
                        Walk(property.Value);
                    }

                    break;
                case JsonValueKind.Array:
                    foreach (var item in e.EnumerateArray())
                    {
                        Walk(item);
                    }

                    break;
                case JsonValueKind.String:
                    builder.Append(e.GetString()).Append(' ');
                    break;
            }
        }

        Walk(element);
        return builder.ToString();
    }

    /// <summary>Puts a kind back to an earlier version (published), so tests leave the shared site as they found it.</summary>
    private static Task<SiteKindBody> RestoreAsync(HttpClient admin, string kind, int version) =>
        SendOkAsync<SiteKindBody>(admin, HttpMethod.Post, Url(kind, $"/restore/{version}"), new { publish = true });

    private static JsonElement PublicDocument(HttpClient client, out HttpResponseMessage response)
    {
        response = client.GetAsync("/api/v1/catalog/site").GetAwaiter().GetResult();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult()).RootElement.GetProperty("result").Clone();
    }

    private static byte[] Png(int width, int height, int totalLength = 0)
    {
        var bytes = new byte[Math.Max(totalLength, 33)];
        byte[] head = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R'];
        head.CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20), (uint)height);
        return bytes;
    }

    private static byte[] Jpeg(int width, int height) =>
        [0xFF, 0xD8, 0xFF, 0xC0, 0x00, 0x0B, 8, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 1, 1, 0x11, 0, 0xFF, 0xD9];

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient admin, byte[] bytes, string fileName, string contentType, string purpose, string? alt = null)
    {
        var csrf = await CsrfTokenAsync(admin);
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        content.Add(file, "file", fileName);
        content.Add(new StringContent(purpose), "purpose");
        if (alt is not null)
        {
            content.Add(new StringContent(alt), "alt");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/site/images") { Content = content };
        request.Headers.Add(csrf.HeaderName, csrf.Token);
        return await admin.SendAsync(request);
    }

    private static async Task<SiteImageBody> UploadOkAsync(HttpClient admin, byte[] bytes, string purpose, string fileName = "x.png", string contentType = "image/png")
    {
        var response = await UploadAsync(admin, bytes, fileName, contentType, purpose);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<SiteImageBody>(response);
    }

    // ---------------------------------------------------------------- seed

    [Fact]
    public async Task EveryKind_IsSeededAsPublishedVersionOne_WithTheApprovedDefaults()
    {
        var admin = await LoginAdminAsync();

        var overview = await GetOkAsync<JsonElement>(admin, "/api/v1/admin/site");
        var kinds = overview.EnumerateArray().Select(e => e.GetProperty("kind").GetString()!).ToArray();
        Assert.Equal(["theme", "home", "chrome", "seo", "badges"], kinds);
        Assert.All(overview.EnumerateArray(), e => Assert.NotEqual(JsonValueKind.Null, e.GetProperty("publishedVersion").ValueKind));

        var document = PublicDocument(NewClient(), out var response);
        Assert.Equal("public, max-age=30", response.Headers.CacheControl?.ToString());

        var theme = document.GetProperty("theme");
        Assert.Equal("#5b0057", theme.GetProperty("primary").GetString());
        Assert.Equal("#d8c831", theme.GetProperty("accent").GetString());
        Assert.Equal("vazirmatn", theme.GetProperty("font").GetString());
        Assert.Equal("sm", theme.GetProperty("radius").GetString());
        var logo = theme.GetProperty("logo");
        Assert.Equal("قصر روشنایی", logo.GetProperty("alt").GetString());
        Assert.Equal(300, logo.GetProperty("width").GetInt32());
        var favicon = theme.GetProperty("favicon");
        Assert.Equal(512, favicon.GetProperty("width").GetInt32());
        foreach (var url in new[] { logo.GetProperty("url").GetString()!, favicon.GetProperty("url").GetString()! })
        {
            Assert.StartsWith("/media/site/", url, StringComparison.Ordinal);
            var served = await NewClient().GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, served.StatusCode);
            Assert.Equal("image/png", served.Content.Headers.ContentType?.MediaType);
        }

        var blocks = document.GetProperty("home").GetProperty("blocks").EnumerateArray().Select(b => b.GetProperty("type").GetString()).ToArray();
        Assert.Contains("hero", blocks);
        Assert.Contains("productRow", blocks);
        Assert.Contains("text", blocks);

        var chrome = document.GetProperty("chrome");
        Assert.False(chrome.GetProperty("announcement").GetProperty("enabled").GetBoolean());
        Assert.Equal(["/", "/", "/about", "/contact"], chrome.GetProperty("headerMenu").EnumerateArray().Select(m => m.GetProperty("link").GetString()!).ToArray());
        var social = chrome.GetProperty("social");
        Assert.Equal("https://x.com/e_ghasr", social.GetProperty("twitter").GetString());
        Assert.False(social.TryGetProperty("instagram", out _));
        Assert.False(social.TryGetProperty("telegram", out _)); // only non-empty entries

        Assert.Equal("%s | قصر روشنایی", document.GetProperty("seo").GetProperty("titleTemplate").GetString());
        Assert.Equal(JsonValueKind.Null, document.GetProperty("badges").GetProperty("enamad").ValueKind);
        Assert.False(string.IsNullOrEmpty(document.GetProperty("version").GetString()));
    }

    [Fact]
    public async Task Seeding_IsIdempotent_ASecondStartupChangesNothing()
    {
        var admin = await LoginAdminAsync();
        var before = await Fixture.WithDbAsync(async db => (await db.SiteConfigVersions.AsNoTracking().OrderBy(v => v.Id).Select(v => new { v.Id, v.Payload, v.Version, v.State }).ToListAsync(),
            await db.SiteMedia.CountAsync()));

        using (var scope = Fixture.Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(CancellationToken.None);
        }

        var after = await Fixture.WithDbAsync(async db => (await db.SiteConfigVersions.AsNoTracking().OrderBy(v => v.Id).Select(v => new { v.Id, v.Payload, v.Version, v.State }).ToListAsync(),
            await db.SiteMedia.CountAsync()));
        Assert.Equal(before.Item1, after.Item1);
        Assert.Equal(before.Item2, after.Item2);
        Assert.NotNull((await GetKindAsync(admin, "theme")).Published);
    }

    // ---------------------------------------------------------------- draft → publish → restore → discard

    [Fact]
    public async Task Draft_Publish_Restore_Discard_RoundTrip()
    {
        var admin = await LoginAdminAsync();
        var start = await CleanAsync(admin, "seo");
        var firstVersion = start.Published!.Version;

        var saved = await SaveOkAsync(admin, "seo", Seo("نسخهٔ الف"));
        Assert.Equal("نسخهٔ الف", saved.Draft!.Payload.GetProperty("homeTitle").GetString());
        Assert.Equal(firstVersion, saved.Published!.Version); // saving a draft does not publish
        Assert.NotEqual("نسخهٔ الف", PublicDocument(NewClient(), out _).GetProperty("seo").GetProperty("homeTitle").GetString());

        var edited = await SaveOkAsync(admin, "seo", Seo("نسخهٔ ب"), saved.Draft.RowVersion);
        Assert.NotEqual(saved.Draft.RowVersion, edited.Draft!.RowVersion);

        var published = await PublishOkAsync(admin, "seo", edited.Draft.RowVersion);
        Assert.Null(published.Draft);
        Assert.Equal(firstVersion + 1, published.Published!.Version);
        Assert.Equal("نسخهٔ ب", published.Published.Payload.GetProperty("homeTitle").GetString());
        Assert.Equal("archived", published.History.Single(h => h.Version == firstVersion).State);
        Assert.Equal("published", published.History[0].State);
        Assert.Equal("نسخهٔ ب", PublicDocument(NewClient(), out _).GetProperty("seo").GetProperty("homeTitle").GetString());

        // Restore the old version into the draft (no publish), then discard it.
        var restored = await SendOkAsync<SiteKindBody>(admin, HttpMethod.Post, Url("seo", $"/restore/{firstVersion}"), new { publish = false });
        Assert.NotNull(restored.Draft);
        Assert.Equal(firstVersion + 1, restored.Published!.Version);
        var discarded = await SendOkAsync<SiteKindBody>(admin, HttpMethod.Post, Url("seo", "/discard"), new { rowVersion = restored.Draft!.RowVersion });
        Assert.Null(discarded.Draft);

        // Restore and publish in one step: the previous live version is itself archived and restorable.
        var back = await SendOkAsync<SiteKindBody>(admin, HttpMethod.Post, Url("seo", $"/restore/{firstVersion}"), new { publish = true });
        Assert.Null(back.Draft);
        Assert.Equal(firstVersion + 2, back.Published!.Version);
        Assert.Equal(start.Published.Payload.GetProperty("homeTitle").GetString(), back.Published.Payload.GetProperty("homeTitle").GetString());
        Assert.Equal("archived", back.History.Single(h => h.Version == firstVersion + 1).State);

        var version = await GetOkAsync<JsonElement>(admin, Url("seo", $"/versions/{firstVersion + 1}"));
        Assert.Equal("نسخهٔ ب", version.GetProperty("payload").GetProperty("homeTitle").GetString());
    }

    [Fact]
    public async Task PublishWithoutADraft_Discard_AndUnknownThings_AreNotFound()
    {
        var admin = await LoginAdminAsync();
        await CleanAsync(admin, "seo");

        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Post, Url("seo", "/publish"), new { rowVersion = new byte[8] }), 404, "draft_not_found");
        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Post, Url("seo", "/discard"), new { rowVersion = new byte[8] }), 404, "draft_not_found");
        await AssertProblemAsync(await admin.GetAsync(Url("fonts")), 404, "site_kind_not_found");
        await AssertProblemAsync(await SaveAsync(admin, "fonts", Seo()), 404, "site_kind_not_found");
        await AssertProblemAsync(await admin.GetAsync(Url("seo", "/versions/9999")), 404, "version_not_found");
        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Post, Url("seo", "/restore/9999"), new { publish = false }), 404, "version_not_found");
    }

    // ---------------------------------------------------------------- concurrency

    [Fact]
    public async Task TwoEditors_TheSecondSaveWithAStaleVersionIs409_AndNothingIsOverwritten()
    {
        var a = await LoginAdminAsync();
        var b = await LoginAdminAsync();
        await CleanAsync(a, "seo");
        var created = await SaveOkAsync(a, "seo", Seo("آغاز"));
        var staleVersion = created.Draft!.RowVersion;

        await SaveOkAsync(a, "seo", Seo("ویرایش الف"), staleVersion); // A saves first

        var stale = await SaveAsync(b, "seo", Seo("ویرایش ب"), staleVersion);
        await AssertProblemAsync(stale, 409, "concurrent_update");
        Assert.Equal("ویرایش الف", (await GetKindAsync(a, "seo")).Draft!.Payload.GetProperty("homeTitle").GetString());

        // A stale publish and a stale discard are refused as well.
        await AssertProblemAsync(await SendAsync(b, HttpMethod.Post, Url("seo", "/publish"), new { rowVersion = staleVersion }), 409, "concurrent_update");
        await AssertProblemAsync(await SendAsync(b, HttpMethod.Post, Url("seo", "/discard"), new { rowVersion = staleVersion }), 409, "concurrent_update");
        Assert.NotNull((await GetKindAsync(a, "seo")).Draft);
        await CleanAsync(a, "seo");
    }

    [Fact]
    public async Task SavingWithoutAVersion_WhenADraftExists_Is409_AndAVersionWithoutADraftIs409()
    {
        var admin = await LoginAdminAsync();
        await CleanAsync(admin, "seo");
        var created = await SaveOkAsync(admin, "seo", Seo("آغاز"));

        await AssertProblemAsync(await SaveAsync(admin, "seo", Seo("بدون نسخه")), 409, "concurrent_update");

        await SendOkAsync<SiteKindBody>(admin, HttpMethod.Post, Url("seo", "/discard"), new { rowVersion = created.Draft!.RowVersion });
        await AssertProblemAsync(await SaveAsync(admin, "seo", Seo("نسخهٔ قدیمی"), created.Draft.RowVersion), 409, "concurrent_update");
    }

    [Fact]
    public async Task TwoFirstSavesAtTheSameTime_ExactlyOneCreatesTheDraft_TheOtherGets409()
    {
        var a = await LoginAdminAsync();
        var b = await LoginAdminAsync();
        await CleanAsync(a, "badges");
        var payload = new { enamad = (object?)null, samandehi = (object?)null };

        var results = await Task.WhenAll(SaveAsync(a, "badges", payload), SaveAsync(b, "badges", payload));

        var codes = results.Select(r => (int)r.StatusCode).Order().ToArray();
        Assert.Equal([200, 409], codes);
        Assert.Equal(1, await Fixture.WithDbAsync(db => db.SiteConfigVersions.CountAsync(v => v.Kind == SiteKind.Badges && v.State == SiteState.Draft)));
        await CleanAsync(a, "badges");
    }

    // ---------------------------------------------------------------- security

    [Fact]
    public async Task Endpoints_RequireLogin_AndTheSiteManagePermission()
    {
        var anonymous = NewClient();
        foreach (var url in new[] { "/api/v1/admin/site", Url("seo"), "/api/v1/admin/site/audit", "/api/v1/admin/site/compiled?state=draft" })
        {
            await AssertProblemAsync(await anonymous.GetAsync(url), 401);
        }

        var (customer, _, _) = await RegisterCustomerAsync();
        foreach (var url in new[] { "/api/v1/admin/site", Url("seo"), "/api/v1/admin/site/audit", "/api/v1/admin/site/compiled?state=draft" })
        {
            await AssertProblemAsync(await customer.GetAsync(url), 403);
        }

        await AssertProblemAsync(await SaveAsync(customer, "seo", Seo()), 403);
        await AssertProblemAsync(await SendAsync(customer, HttpMethod.Post, Url("seo", "/publish"), new { rowVersion = new byte[8] }), 403);
        await AssertProblemAsync(await UploadAsync(customer, Png(10, 10), "a.png", "image/png", "logo"), 403);
        Assert.Equal(HttpStatusCode.OK, (await (await LoginAdminAsync()).GetAsync("/api/v1/admin/site")).StatusCode);
    }

    [Fact]
    public async Task Writes_WithoutTheAntiforgeryToken_AreRejected()
    {
        var admin = await LoginAdminAsync();
        await CleanAsync(admin, "seo");

        using var put = new HttpRequestMessage(HttpMethod.Put, Url("seo", "/draft")) { Content = JsonContent.Create(new { payload = Seo(), rowVersion = (byte[]?)null }, options: ApiJson) };
        await AssertProblemAsync(await admin.SendAsync(put), 400, "antiforgery_invalid");
        using var publish = new HttpRequestMessage(HttpMethod.Post, Url("seo", "/publish")) { Content = JsonContent.Create(new { rowVersion = new byte[8] }, options: ApiJson) };
        await AssertProblemAsync(await admin.SendAsync(publish), 400, "antiforgery_invalid");

        var csrf = await CsrfTokenAsync(admin);
        using var upload = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/site/images");
        using var content = new MultipartFormDataContent { { new ByteArrayContent(Png(10, 10)), "file", "a.png" }, { new StringContent("logo"), "purpose" } };
        upload.Content = content;
        await AssertProblemAsync(await admin.SendAsync(upload), 400, "antiforgery_invalid");
        Assert.Null((await GetKindAsync(admin, "seo")).Draft);
        Assert.NotNull(csrf);
    }

    // ---------------------------------------------------------------- validation

    [Fact]
    public async Task Theme_ContrastFailure_IsReportedOnSave_ButBlocksPublish()
    {
        var admin = await LoginAdminAsync();
        var startVersion = (await CleanAsync(admin, "theme")).Published!.Version;

        var saved = await SaveOkAsync(admin, "theme", Theme(text: "#aaaaaa")); // 2.3 : 1 on white
        var failing = saved.Report!.Value.GetProperty("contrast").EnumerateArray().Where(r => !r.GetProperty("ok").GetBoolean()).Select(r => r.GetProperty("id").GetString()).ToArray();
        Assert.Contains("text-background", failing);
        Assert.Contains("text-surface", failing);

        var blocked = await AssertProblemAsync(await SendAsync(admin, HttpMethod.Post, Url("theme", "/publish"), new { rowVersion = saved.Draft!.RowVersion }), 400, "validation_failed");
        Assert.Contains("contrast", blocked.Errors!.Keys);
        Assert.Contains(blocked.Errors["contrast"], m => m.Contains("متن روی پس‌زمینه") && m.Contains("حداقل ۴٫۵"));
        Assert.Equal("#1a1a1a", PublicDocument(NewClient(), out _).GetProperty("theme").GetProperty("text").GetString());

        var fixedUp = await SaveOkAsync(admin, "theme", Theme(), saved.Draft.RowVersion);
        Assert.All(fixedUp.Report!.Value.GetProperty("contrast").EnumerateArray(), r => Assert.True(r.GetProperty("ok").GetBoolean()));
        await PublishOkAsync(admin, "theme", fixedUp.Draft!.RowVersion);
        await RestoreAsync(admin, "theme", startVersion);
    }

    [Fact]
    public async Task Theme_PublishedWithAnAllowListedFont_ReachesThePublicDocument()
    {
        var admin = await LoginAdminAsync();
        await CleanAsync(admin, "theme");
        var original = (await GetKindAsync(admin, "theme")).Published!.Payload;

        var saved = await SaveOkAsync(admin, "theme", Theme(font: "estedad"));
        await PublishOkAsync(admin, "theme", saved.Draft!.RowVersion);
        Assert.Equal("estedad", PublicDocument(NewClient(), out _).GetProperty("theme").GetProperty("font").GetString());

        var back = await SaveOkAsync(admin, "theme", JsonSerializer.Deserialize<JsonElement>(original.GetRawText()));
        await PublishOkAsync(admin, "theme", back.Draft!.RowVersion);
    }

    public static TheoryData<string, string, object, string> InvalidPayloads => new()
    {
        { "theme", "primary", new { primary = "red", secondary = "#76007c", accent = "#d8c831", background = "#ffffff", surface = "#f2f2f2", text = "#1a1a1a", success = "#1b7f3b", warning = "#8a5a00", danger = "#b3261e", font = "vazirmatn", radius = "sm" }, "bad hex" },
        { "theme", "font", new { primary = "#5b0057", secondary = "#76007c", accent = "#d8c831", background = "#ffffff", surface = "#f2f2f2", text = "#1a1a1a", success = "#1b7f3b", warning = "#8a5a00", danger = "#b3261e", font = "IRANSans", radius = "sm" }, "commercial font" },
        { "theme", "radius", new { primary = "#5b0057", secondary = "#76007c", accent = "#d8c831", background = "#ffffff", surface = "#f2f2f2", text = "#1a1a1a", success = "#1b7f3b", warning = "#8a5a00", danger = "#b3261e", font = "vazirmatn", radius = "xl" }, "bad radius" },
        { "theme", "logo.mediaId", new { primary = "#5b0057", secondary = "#76007c", accent = "#d8c831", background = "#ffffff", surface = "#f2f2f2", text = "#1a1a1a", success = "#1b7f3b", warning = "#8a5a00", danger = "#b3261e", font = "vazirmatn", radius = "sm", logo = new { mediaId = 999999, alt = "x" } }, "unknown media id" },
        { "seo", "titleTemplate", new { siteName = "قصر", titleTemplate = "بدون جانشین", homeTitle = "x", defaultDescription = "y" }, "template without %s" },
        { "seo", "defaultOgMediaId", new { siteName = "قصر", titleTemplate = "%s", homeTitle = "x", defaultDescription = "y", defaultOgMediaId = 999999 }, "unknown og image" },
        { "chrome", "announcement.link", new { announcement = new { enabled = true, text = "t", link = "javascript:alert(1)", style = "primary", dismissible = true } }, "javascript: link" },
        { "chrome", "headerMenu[0].link", new { announcement = new { enabled = false, text = "", style = "primary", dismissible = true }, headerMenu = new object[] { new { label = "x", link = "//evil.com" } } }, "protocol-relative link" },
        { "chrome", "footer.columns[0].links[0].link", new { announcement = new { enabled = false, text = "", style = "primary", dismissible = true }, footer = new { about = "", columns = new object[] { new { title = "c", links = new object[] { new { label = "l", link = "http://example.com" } } } } } }, "http link" },
        { "chrome", "social.twitter", new { announcement = new { enabled = false, text = "", style = "primary", dismissible = true }, social = new { twitter = "https://evil.com/x" } }, "foreign social host" },
        { "badges", "enamad.code", new { enamad = new { id = "123", code = "<script>alert(1)</script>" } }, "pasted html as badge code" },
        { "badges", "samandehi.verifyCode", new { samandehi = new { id = "123", verifyCode = "short", logoCode = "abcdefghij1" } }, "short samandehi token" },
        { "home", "blocks[0].data.mediaId", new { blocks = new object[] { new { id = Guid.NewGuid(), type = "banner", enabled = true, data = new { mediaId = 424242, alt = "a", height = "md" } } } }, "banner with unknown media" },
        { "home", "blocks[0].data.slides[0].link", new { blocks = new object[] { new { id = Guid.NewGuid(), type = "hero", enabled = true, data = new { autoplaySeconds = 0, slides = new object[] { new { title = "t", linkLabel = "l", link = "javascript:x" } } } } } }, "hero javascript link" },
        { "home", "blocks[0].data.items[1].categoryId", new { blocks = new object[] { new { id = Guid.NewGuid(), type = "categoryTiles", enabled = true, data = new { items = new object[] { new { categoryId = 999991 }, new { categoryId = 999992 } } } } } }, "unknown categories" },
        { "home", "blocks[0].data.productIds", new { blocks = new object[] { new { id = Guid.NewGuid(), type = "productRow", enabled = true, data = new { title = "t", source = "handpicked", productIds = new[] { 999991 }, limit = 4 } } } }, "unknown product" },
    };

    [Theory]
    [MemberData(nameof(InvalidPayloads))]
    public async Task InvalidPayloads_AreRejected_WithTheFieldPath_AndNothingIsStored(string kind, string errorKey, object payload, string why)
    {
        var admin = await LoginAdminAsync();
        await CleanAsync(admin, kind);

        var response = await SaveAsync(admin, kind, payload);

        var problem = await AssertProblemAsync(response, 400, "validation_failed");
        Assert.True(problem.Errors!.ContainsKey(errorKey), $"{why}: expected key {errorKey}, got {string.Join(", ", problem.Errors.Keys)}");
        Assert.All(problem.Errors[errorKey], message => Assert.Matches("[؀-ۿ]", message)); // Persian, never English
        Assert.Null((await GetKindAsync(admin, kind)).Draft);
    }

    [Fact]
    public async Task Home_MoreThanTwentyBlocks_IsRejected_AndTwentyIsAccepted()
    {
        var admin = await LoginAdminAsync();
        await CleanAsync(admin, "home");
        var twentyOne = Enumerable.Range(0, 21).Select(i => TextBlock("بخش " + i, "متن")).ToArray();

        var tooMany = await AssertProblemAsync(await SaveAsync(admin, "home", Home(twentyOne)), 400, "validation_failed");
        Assert.Contains("blocks", tooMany.Errors!.Keys);

        var saved = await SaveOkAsync(admin, "home", Home(twentyOne.Take(20).ToArray()));
        Assert.Equal(20, saved.Draft!.Payload.GetProperty("blocks").GetArrayLength());
        await CleanAsync(admin, "home");
    }

    [Fact]
    public async Task Payload_WithTheWrongShape_Is400_NotA500()
    {
        var admin = await LoginAdminAsync();
        await CleanAsync(admin, "seo");

        foreach (var body in new object[] { new { payload = "text", rowVersion = (byte[]?)null }, new { payload = new[] { 1, 2 }, rowVersion = (byte[]?)null }, new { rowVersion = (byte[]?)null }, new { payload = new { siteName = 5 } } })
        {
            var response = await SendAsync(admin, HttpMethod.Put, Url("seo", "/draft"), body);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        Assert.Null((await GetKindAsync(admin, "seo")).Draft);
    }

    [Fact]
    public async Task Home_UnknownBlockTypes_AreDropped_AndTextIsStoredAsText()
    {
        var admin = await LoginAdminAsync();
        await CleanAsync(admin, "home");
        const string script = "<script>alert('x')</script>";

        var saved = await SaveOkAsync(admin, "home", Home(
            new { id = Guid.NewGuid(), type = "carousel3d", enabled = true, data = new { x = 1 } },
            TextBlock(script, "**متن** " + script)));

        var blocks = saved.Draft!.Payload.GetProperty("blocks");
        Assert.Equal(1, blocks.GetArrayLength());
        Assert.Equal("text", blocks[0].GetProperty("type").GetString());
        Assert.Equal(script, blocks[0].GetProperty("data").GetProperty("heading").GetString()); // stored verbatim as text; the storefront renders it as text
        await CleanAsync(admin, "home");
    }

    // ---------------------------------------------------------------- public document

    [Fact]
    public async Task PublicDocument_ShowsOnlyPublishedData_HidesDisabledBlocks_AndUpdatesOnPublish()
    {
        var admin = await LoginAdminAsync();
        var startVersion = (await CleanAsync(admin, "home")).Published!.Version;
        var marker = "نشانگر-" + Guid.NewGuid().ToString("N")[..8];
        var hidden = "پنهان-" + Guid.NewGuid().ToString("N")[..8];

        var saved = await SaveOkAsync(admin, "home", Home(TextBlock(marker, "متن"), TextBlock(hidden, "متن", enabled: false)));
        Assert.DoesNotContain(marker, Text(PublicDocument(NewClient(), out _)));

        await PublishOkAsync(admin, "home", saved.Draft!.RowVersion);
        var after = PublicDocument(NewClient(), out _);
        var headings = after.GetProperty("home").GetProperty("blocks").EnumerateArray().Select(b => b.GetProperty("data").GetProperty("heading").GetString()!).ToArray();
        Assert.Equal([marker], headings);
        Assert.DoesNotContain(hidden, Text(after));

        // The preview document applies the draft; the public one does not.
        var draft = await SaveOkAsync(admin, "home", Home(TextBlock("فقط-پیش‌نویس-" + marker, "متن")));
        var preview = await GetOkAsync<JsonElement>(admin, "/api/v1/admin/site/compiled?state=draft");
        Assert.Contains("فقط-پیش‌نویس-" + marker, Text(preview));
        Assert.StartsWith("draft-", preview.GetProperty("version").GetString());
        Assert.DoesNotContain("فقط-پیش‌نویس-", Text(PublicDocument(NewClient(), out _)));
        Assert.DoesNotContain("فقط-پیش‌نویس-", Text(await GetOkAsync<JsonElement>(admin, "/api/v1/admin/site/compiled?state=published")));
        await AssertProblemAsync(await admin.GetAsync("/api/v1/admin/site/compiled?state=other"), 400);
        await SendOkAsync<SiteKindBody>(admin, HttpMethod.Post, Url("home", "/discard"), new { rowVersion = draft.Draft!.RowVersion });
        await RestoreAsync(admin, "home", startVersion);
    }

    [Fact]
    public async Task PublicDocument_ResolvesCategoriesAndProducts_AndDropsWhatNoLongerExists()
    {
        var admin = await LoginAdminAsync();
        var startVersion = (await CleanAsync(admin, "home")).Published!.Version;
        var slugA = Unique("tilea");
        var slugB = Unique("tileb");
        var categoryA = (await CreateCategoryAsync(admin, slugA)).Id;
        var categoryB = (await CreateCategoryAsync(admin, slugB)).Id;
        var inactive = (await CreateCategoryAsync(admin, Unique("off"), isActive: false)).Id;
        var product = await CreateProductAsync(admin, categoryA, Unique("pick"));
        var draftProduct = await CreateProductAsync(admin, categoryA, Unique("draftpick"), status: "draft");

        var saved = await SaveOkAsync(admin, "home", Home(
            new { id = Guid.NewGuid(), type = "categoryTiles", enabled = true, data = new { items = new object[] { new { categoryId = categoryA, label = "نمای ویژه" }, new { categoryId = categoryB } } } },
            new { id = Guid.NewGuid(), type = "productRow", enabled = true, data = new { title = "منتخب", source = "handpicked", productIds = new[] { product.Id, draftProduct.Id }, limit = 4 } },
            new { id = Guid.NewGuid(), type = "productRow", enabled = true, data = new { title = "دسته", source = "category", categoryId = categoryB, limit = 8 } },
            new { id = Guid.NewGuid(), type = "productRow", enabled = true, data = new { title = "تخفیف", source = "discounted", limit = 8 } }));
        var myPayload = saved.Draft!.Payload;
        await PublishOkAsync(admin, "home", saved.Draft.RowVersion);

        var blocks = PublicDocument(NewClient(), out _).GetProperty("home").GetProperty("blocks").EnumerateArray().ToArray();
        var tiles = blocks[0].GetProperty("data").GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal([slugA, slugB], tiles.Select(t => t.GetProperty("slug").GetString()!).ToArray());
        Assert.Equal("نمای ویژه", tiles[0].GetProperty("label").GetString());
        Assert.Equal("دسته " + slugB, tiles[1].GetProperty("label").GetString());

        var picked = blocks[1].GetProperty("data");
        Assert.Equal([product.Slug], picked.GetProperty("productSlugs").EnumerateArray().Select(s => s.GetString()!).ToArray()); // the draft product is skipped
        var byCategory = blocks[2].GetProperty("data");
        Assert.Equal(slugB, byCategory.GetProperty("categorySlug").GetString());
        Assert.Equal($"/category/{slugB}", byCategory.GetProperty("viewAllHref").GetString());
        Assert.Equal("discounted", blocks[3].GetProperty("data").GetProperty("source").GetString());

        // A category that is switched off later disappears from the document without breaking it.
        await SendOkAsync<IdBody>(admin, HttpMethod.Put, $"/api/v1/admin/categories/{categoryB}", new { name = "دسته " + slugB, parentId = (int?)null, sortOrder = 0, isActive = false });
        // The document is cached for the life of the published version, so touch it with a new publish.
        var again = await SaveOkAsync(admin, "home", myPayload);
        await PublishOkAsync(admin, "home", again.Draft!.RowVersion);
        var remaining = PublicDocument(NewClient(), out _).GetProperty("home").GetProperty("blocks").EnumerateArray().Select(b => b.GetProperty("type").GetString()).ToArray();
        Assert.DoesNotContain("categoryTiles", remaining); // only one tile left: the block is dropped
        Assert.Equal(2, remaining.Count(t => t == "productRow")); // the category row is gone too
        Assert.NotEqual(0, inactive);
        await RestoreAsync(admin, "home", startVersion);
    }

    // ---------------------------------------------------------------- versions: pruning, media, audit

    [Fact]
    public async Task OnlyTheNewestTwentyVersionsAreKept_AndImagesOfPrunedVersionsStay()
    {
        var admin = await LoginAdminAsync();
        var original = (await CleanAsync(admin, "seo")).Published!.Payload;
        var image = await UploadOkAsync(admin, Png(1300, 700), "og");
        var first = await SaveOkAsync(admin, "seo", Seo("با تصویر", image.Id));
        var withImage = await PublishOkAsync(admin, "seo", first.Draft!.RowVersion);
        var imageVersion = withImage.Published!.Version;
        Assert.Equal(image.Url, withImage.Media[image.Id.ToString()].Url);

        SiteKindBody current = withImage;
        for (var i = 0; i < 22; i++)
        {
            var draft = await SaveOkAsync(admin, "seo", Seo("نسخه " + i));
            current = await PublishOkAsync(admin, "seo", draft.Draft!.RowVersion);
        }

        Assert.Equal(20, current.History.Length);
        Assert.Equal(current.Published!.Version, current.History[0].Version);
        Assert.Equal(current.History[0].Version - 19, current.History[^1].Version);
        Assert.Single(current.History, h => h.State == "published");
        await AssertProblemAsync(await admin.GetAsync(Url("seo", $"/versions/{imageVersion}")), 404, "version_not_found");

        // The pruned version is gone, its image is not.
        Assert.Equal(1, await Fixture.WithDbAsync(db => db.SiteMedia.CountAsync(m => m.Id == image.Id)));
        var served = await NewClient().GetAsync(image.Url);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal(
            20,
            await Fixture.WithDbAsync(db => db.SiteConfigVersions.CountAsync(v => v.Kind == SiteKind.Seo && v.Version != null)));

        var restored = await SaveOkAsync(admin, "seo", JsonSerializer.Deserialize<JsonElement>(original.GetRawText()));
        await PublishOkAsync(admin, "seo", restored.Draft!.RowVersion);
    }

    [Fact]
    public async Task Audit_RecordsWhoAndWhat_ButNeverThePayloadText()
    {
        var admin = await LoginAdminAsync();
        var startVersion = (await CleanAsync(admin, "seo")).Published!.Version;
        var secret = "راز-" + Guid.NewGuid().ToString("N");

        var saved = await SaveOkAsync(admin, "seo", Seo(secret));
        await PublishOkAsync(admin, "seo", saved.Draft!.RowVersion);

        var page = await GetOkAsync<SiteAuditPageBody>(admin, "/api/v1/admin/site/audit?kind=seo");
        var entries = page.Items.Take(3).ToArray();
        Assert.Contains(entries, e => e.Action == "saved" && e.Summary.Contains("homeTitle"));
        Assert.Contains(entries, e => e.Action == "published" && e.Version is not null);
        Assert.All(entries, e => Assert.Equal(ApiFixture.AdminEmail, e.AdminUsername));
        Assert.All(page.Items, e =>
        {
            Assert.DoesNotContain(secret, e.Summary);
            Assert.Equal("seo", e.Kind);
        });
        var everything = await GetOkAsync<SiteAuditPageBody>(admin, "/api/v1/admin/site/audit");
        Assert.DoesNotContain(everything.Items, e => e.Summary.Contains(secret));
        Assert.True(everything.Total >= page.Total);
        await AssertProblemAsync(await admin.GetAsync("/api/v1/admin/site/audit?kind=nope"), 400);
        Assert.Contains(await Fixture.WithDbAsync(db => db.SiteAuditLogs.AsNoTracking().Select(a => a.Summary).ToListAsync()), s => s.Length > 0);
        Assert.DoesNotContain(await Fixture.WithDbAsync(db => db.SiteAuditLogs.AsNoTracking().Select(a => a.Summary).ToListAsync()), s => s.Contains(secret));
        await RestoreAsync(admin, "seo", startVersion);
    }

    [Fact]
    public async Task EveryDraftSavePublishRestoreAndDiscard_IsAudited()
    {
        var admin = await LoginAdminAsync();
        var startVersion = (await CleanAsync(admin, "badges")).Published!.Version;
        var before = await Fixture.WithDbAsync(db => db.SiteAuditLogs.CountAsync(a => a.Kind == SiteKind.Badges));

        var saved = await SaveOkAsync(admin, "badges", new { enamad = new { id = "12345", code = "AbCdEfGhIjKl" }, samandehi = (object?)null });
        var published = await PublishOkAsync(admin, "badges", saved.Draft!.RowVersion);
        var restore = await SendOkAsync<SiteKindBody>(admin, HttpMethod.Post, Url("badges", $"/restore/{published.History.Last().Version}"), new { publish = false });
        await SendOkAsync<SiteKindBody>(admin, HttpMethod.Post, Url("badges", "/discard"), new { rowVersion = restore.Draft!.RowVersion });

        var actions = (await Fixture.WithDbAsync(db => db.SiteAuditLogs.AsNoTracking().Where(a => a.Kind == SiteKind.Badges).OrderBy(a => a.Id).Select(a => a.Action).ToListAsync()))
            .Skip(before).ToArray();
        Assert.Equal([SiteAuditAction.Saved, SiteAuditAction.Published, SiteAuditAction.Restored, SiteAuditAction.Discarded], actions);
        var doc = PublicDocument(NewClient(), out _).GetProperty("badges");
        Assert.Equal("12345", doc.GetProperty("enamad").GetProperty("id").GetString());
        Assert.Equal(JsonValueKind.Null, doc.GetProperty("samandehi").ValueKind);
        await RestoreAsync(admin, "badges", startVersion);
    }

    // ---------------------------------------------------------------- image upload

    [Fact]
    public async Task Upload_StoresARealImage_ReadsItsSize_AndServesIt()
    {
        var admin = await LoginAdminAsync();

        var logo = await UploadOkAsync(admin, Png(300, 70), "logo");
        Assert.Equal((300, 70, "logo"), (logo.Width, logo.Height, logo.Purpose));
        Assert.StartsWith("/media/site/", logo.Url, StringComparison.Ordinal);
        Assert.EndsWith(".png", logo.Url, StringComparison.Ordinal);
        Assert.Empty(logo.Warnings);

        var narrow = await UploadOkAsync(admin, Jpeg(800, 400), "hero", "wide.jpg", "image/jpeg");
        Assert.Equal(["narrow_image"], narrow.Warnings);
        Assert.EndsWith(".jpg", narrow.Url, StringComparison.Ordinal);
        var wide = await UploadOkAsync(admin, Png(1600, 600), "banner");
        Assert.Empty(wide.Warnings);

        var served = await NewClient().GetAsync(logo.Url);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);
        Assert.Equal(Png(300, 70), await served.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Upload_OnlyTheBytesDecide_NotTheNameOrTheContentType()
    {
        var admin = await LoginAdminAsync();

        // A real PNG with a misleading name and type is a PNG, stored under a generated name with the right extension.
        var disguised = await UploadOkAsync(admin, Png(64, 64), "hero", "../../evil.svg", "image/svg+xml");
        Assert.EndsWith(".png", disguised.Url, StringComparison.Ordinal);
        Assert.DoesNotContain("evil", disguised.Url);

        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"><script>alert(1)</script></svg>";
        foreach (var (bytes, name, type) in new[]
                 {
                     (System.Text.Encoding.UTF8.GetBytes(svg), "logo.svg", "image/svg+xml"),
                     (System.Text.Encoding.UTF8.GetBytes(svg), "logo.png", "image/png"),
                     (System.Text.Encoding.UTF8.GetBytes("<html><script>alert(1)</script></html>"), "page.jpg", "image/jpeg"),
                     (System.Text.Encoding.UTF8.GetBytes("GIF89a\u0001\u0000\u0001\u0000"), "a.gif", "image/gif"),
                     (Array.Empty<byte>(), "empty.png", "image/png"),
                 })
        {
            var problem = await AssertProblemAsync(await UploadAsync(admin, bytes, name, type, "logo"), 400);
            Assert.Contains("file", problem.Errors!.Keys);
        }
    }

    [Fact]
    public async Task Upload_Favicon_MustBeASquarePngOfAtLeast192()
    {
        var admin = await LoginAdminAsync();

        Assert.Equal(192, (await UploadOkAsync(admin, Png(192, 192), "favicon")).Width);
        Assert.Equal(512, (await UploadOkAsync(admin, Png(512, 512), "favicon")).Width);
        foreach (var (bytes, type) in new[] { (Png(300, 70), "image/png"), (Png(128, 128), "image/png"), (Png(191, 191), "image/png"), (Jpeg(256, 256), "image/jpeg") })
        {
            var problem = await AssertProblemAsync(await UploadAsync(admin, bytes, "f.png", type, "favicon"), 400);
            Assert.Contains("file", problem.Errors!.Keys);
        }
    }

    [Fact]
    public async Task Upload_LimitIsFiveMegabytes_AndThePurposeIsChecked()
    {
        var admin = await LoginAdminAsync();
        var big = Png(100, 100, 5 * 1024 * 1024 + 1);

        Assert.Equal(HttpStatusCode.Created, (await UploadAsync(admin, big[..^1], "big.png", "image/png", "banner")).StatusCode);
        Assert.Contains("file", (await AssertProblemAsync(await UploadAsync(admin, big, "big.png", "image/png", "banner"), 400)).Errors!.Keys);
        Assert.Contains("purpose", (await AssertProblemAsync(await UploadAsync(admin, Png(10, 10), "a.png", "image/png", "wallpaper"), 400)).Errors!.Keys);
        Assert.Contains("purpose", (await AssertProblemAsync(await UploadAsync(admin, Png(10, 10), "a.png", "image/png", ""), 400)).Errors!.Keys);
    }

    [Fact]
    public async Task UploadedImages_CanBeUsedByThePayloads_WithThePurposeTheyWereUploadedFor()
    {
        var admin = await LoginAdminAsync();
        await CleanAsync(admin, "theme");
        var logo = await UploadOkAsync(admin, Png(300, 70), "logo");
        var hero = await UploadOkAsync(admin, Png(1600, 600), "hero");
        var original = (await GetKindAsync(admin, "theme")).Published!.Payload;

        // A hero image is not a logo.
        var wrong = new { primary = "#5b0057", secondary = "#76007c", accent = "#d8c831", background = "#ffffff", surface = "#f2f2f2", text = "#1a1a1a", success = "#1b7f3b", warning = "#8a5a00", danger = "#b3261e", font = "vazirmatn", radius = "sm", logo = new { mediaId = hero.Id, alt = "x" } };
        Assert.Contains("logo.mediaId", (await AssertProblemAsync(await SaveAsync(admin, "theme", wrong), 400)).Errors!.Keys);

        var good = new { primary = "#5b0057", secondary = "#76007c", accent = "#d8c831", background = "#ffffff", surface = "#f2f2f2", text = "#1a1a1a", success = "#1b7f3b", warning = "#8a5a00", danger = "#b3261e", font = "vazirmatn", radius = "sm", logo = new { mediaId = logo.Id, alt = "نشان فروشگاه" } };
        var saved = await SaveOkAsync(admin, "theme", good);
        Assert.Equal(logo.Url, saved.Media[logo.Id.ToString()].Url);
        await PublishOkAsync(admin, "theme", saved.Draft!.RowVersion);
        Assert.Equal(logo.Url, PublicDocument(NewClient(), out _).GetProperty("theme").GetProperty("logo").GetProperty("url").GetString());

        var back = await SaveOkAsync(admin, "theme", JsonSerializer.Deserialize<JsonElement>(original.GetRawText()));
        await PublishOkAsync(admin, "theme", back.Draft!.RowVersion);
    }
}
