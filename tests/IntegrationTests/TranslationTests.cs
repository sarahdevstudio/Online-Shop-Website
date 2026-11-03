using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Shop.IntegrationTests;

public sealed record TranslationBody(int Id, string Key, string Culture, string Text, string DefaultText, bool IsEdited, bool InUse, string[] Placeholders, string? UpdatedBy, byte[] RowVersion);

public sealed record TranslationPage(TranslationBody[] Items, int Page, int PageSize, int Total);

public sealed record TranslationAuditBody(DateTime AtUtc, string? AdminUsername, string OldText, string NewText);

[Collection(ApiCollection.Name)]
public sealed class TranslationTests(ApiFixture fixture) : StoreTestBase(fixture)
{
    private async Task<TranslationBody> FindAsync(HttpClient admin, string key)
    {
        var page = await GetOkAsync<TranslationPage>(admin, $"/api/v1/admin/translations?q={Uri.EscapeDataString(key)}");
        return page.Items.Single(t => t.Key == key);
    }

    private static Task<HttpResponseMessage> EditAsync(HttpClient admin, TranslationBody row, string text) =>
        SendAsync(admin, HttpMethod.Put, $"/api/v1/admin/translations/{row.Id}", new { text, rowVersion = row.RowVersion });

    private static Task<HttpResponseMessage> ResetAsync(HttpClient admin, TranslationBody row) =>
        SendAsync(admin, HttpMethod.Post, $"/api/v1/admin/translations/{row.Id}/reset", new { rowVersion = row.RowVersion });

    [Fact]
    public async Task EveryManifestKey_IsSeeded_AndTheAreasAreListed()
    {
        var admin = await LoginAdminAsync();
        var all = await GetOkAsync<TranslationPage>(admin, "/api/v1/admin/translations");
        var areas = await GetOkAsync<JsonElement>(admin, "/api/v1/admin/translations/areas");

        Assert.True(all.Total >= Shop.Application.Translations.TranslationManifest.Defaults.Count);
        Assert.All(all.Items, t => Assert.Equal("fa-IR", t.Culture));
        Assert.Contains(areas.EnumerateArray(), a => a.GetProperty("area").GetString() == "api.error");
    }

    [Fact]
    public async Task AnEditedText_AppearsInApiErrors_AndResetBringsTheDefaultBack()
    {
        var admin = await LoginAdminAsync();
        var row = await FindAsync(admin, "api.error.not-found");
        var anon = NewClient();
        try
        {
            var edit = await EditAsync(admin, row, "چیزی با این نشانی نیست.");
            Assert.True(edit.IsSuccessStatusCode, await edit.Content.ReadAsStringAsync());
            var edited = await ReadAsync<TranslationBody>(edit);
            Assert.True(edited.IsEdited);

            var problem = await AssertProblemAsync(await anon.GetAsync("/api/v1/nope-" + Guid.NewGuid().ToString("N")), 404);
            Assert.Equal("چیزی با این نشانی نیست.", problem.Title);

            var audit = await GetOkAsync<TranslationAuditBody[]>(admin, $"/api/v1/admin/translations/{row.Id}/audit");
            Assert.Equal("چیزی با این نشانی نیست.", audit[0].NewText);
            Assert.Equal(row.Text, audit[0].OldText);
            Assert.NotNull(audit[0].AdminUsername);
        }
        finally
        {
            var current = await FindAsync(admin, "api.error.not-found");
            await ResetAsync(admin, current);
        }

        var back = await AssertProblemAsync(await anon.GetAsync("/api/v1/nope-" + Guid.NewGuid().ToString("N")), 404);
        Assert.Equal(row.DefaultText, back.Title);
        Assert.False((await FindAsync(admin, "api.error.not-found")).IsEdited);
    }

    [Fact]
    public async Task AValidationMessageWithPlaceholders_KeepsThemWhenEdited_AndRefusesChangedOnes()
    {
        var admin = await LoginAdminAsync();
        var row = await FindAsync(admin, "api.validation.settings-max-quantity");
        Assert.Contains("max", row.Placeholders);

        var dropped = await EditAsync(admin, row, "حداکثر تعداد معتبر نیست.");
        var problem = await AssertProblemAsync(dropped, 400);
        Assert.Contains("text", problem.Errors!.Keys);
        Assert.Contains("{max}", problem.Errors["text"][0]);

        var unknown = await EditAsync(admin, row, row.Text + " {extra}");
        await AssertProblemAsync(unknown, 400);

        var empty = await EditAsync(admin, row, "   ");
        await AssertProblemAsync(empty, 400);

        Assert.False((await FindAsync(admin, row.Key)).IsEdited);
    }

    [Fact]
    public async Task AStaleRowVersion_Is409_AndNothingIsOverwritten()
    {
        var admin = await LoginAdminAsync();
        var row = await FindAsync(admin, "api.error.unauthorized");
        try
        {
            Assert.True((await EditAsync(admin, row, "ورود لازم است (۱).")).IsSuccessStatusCode);
            await AssertProblemAsync(await EditAsync(admin, row, "ورود لازم است (۲)."), 409);
            Assert.Equal("ورود لازم است (۱).", (await FindAsync(admin, row.Key)).Text);
        }
        finally
        {
            await ResetAsync(admin, await FindAsync(admin, row.Key));
        }
    }

    [Fact]
    public async Task OnlyTheTranslationsPermission_CanEdit_AndWritesNeedCsrf()
    {
        var anon = NewClient();
        await AssertProblemAsync(await anon.GetAsync("/api/v1/admin/translations"), 401);
        await AssertProblemAsync(await anon.GetAsync("/api/v1/admin/translations/bundle"), 401);

        var (customer, _, _) = await RegisterCustomerAsync();
        await AssertProblemAsync(await customer.GetAsync("/api/v1/admin/translations"), 403);
        await AssertProblemAsync(await customer.GetAsync("/api/v1/admin/translations/bundle"), 403);

        var admin = await LoginAdminAsync();
        var row = await FindAsync(admin, "api.error.forbidden");
        using var noCsrf = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/admin/translations/{row.Id}") { Content = JsonContent.Create(new { text = "x", rowVersion = row.RowVersion }) };
        await AssertProblemAsync(await admin.SendAsync(noCsrf), 400, "antiforgery_invalid");
        Assert.False((await FindAsync(admin, row.Key)).IsEdited);
    }

    [Fact]
    public async Task TheBundles_ServeOnlyTheirOwnAreas()
    {
        var anon = NewClient();
        var publicBundle = await GetOkAsync<Dictionary<string, string>>(anon, "/api/v1/catalog/translations");
        Assert.All(publicBundle.Keys, k => Assert.Matches("^(common|shop|seo)\\.", k));
        Assert.DoesNotContain(publicBundle.Keys, k => k.StartsWith("api.") || k.StartsWith("admin."));

        var onlyCart = await GetOkAsync<Dictionary<string, string>>(anon, "/api/v1/catalog/translations?prefix=shop.cart.");
        Assert.All(onlyCart.Keys, k => Assert.StartsWith("shop.cart.", k));

        // Asking for an area the endpoint does not serve gives nothing, not the texts.
        Assert.Empty(await GetOkAsync<Dictionary<string, string>>(anon, "/api/v1/catalog/translations?prefix=api."));
        Assert.Empty(await GetOkAsync<Dictionary<string, string>>(anon, "/api/v1/catalog/translations?prefix=admin."));

        var admin = await LoginAdminAsync();
        var adminBundle = await GetOkAsync<Dictionary<string, string>>(admin, "/api/v1/admin/translations/bundle?prefix=admin.");
        Assert.All(adminBundle.Keys, k => Assert.StartsWith("admin.", k));
        Assert.Empty(await GetOkAsync<Dictionary<string, string>>(admin, "/api/v1/admin/translations/bundle?prefix=api."));
    }
}
