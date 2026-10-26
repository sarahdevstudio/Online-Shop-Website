using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shop.Domain.Content;
using Shop.Infrastructure.Identity;
using Shop.Infrastructure.Persistence;

namespace Shop.IntegrationTests;

public sealed record PageBody(string Slug, string Title, string Body, DateTime UpdatedAtUtc);

public sealed record PageSummaryBody(string Slug, string Title, DateTime UpdatedAtUtc);

public sealed record AdminPageBody(string Slug, string Title, string Body, bool IsDraft, string? UpdatedByEmail, byte[] RowVersion);

public sealed record AdminPageListBody(string Slug, string Title, bool IsDraft, string? UpdatedByEmail);

public sealed record ContentAuditBody(Guid? AdminUserId, string? AdminEmail, bool TitleChanged, bool BodyChanged);

public sealed class ContentTests(ApiFixture fixture) : CatalogTestBase(fixture)
{
    private static readonly string[] Slugs = ["about", "contact", "terms", "returns", "privacy"];

    private async Task<AdminPageBody> PageAsync(HttpClient admin, string slug) =>
        await GetOkAsync<AdminPageBody>(admin, $"/api/v1/admin/content-pages/{slug}");

    private static Task<HttpResponseMessage> PutAsync(HttpClient admin, string slug, string title, string body, byte[] rowVersion) =>
        SendAsync(admin, HttpMethod.Put, $"/api/v1/admin/content-pages/{slug}", new { title, body, rowVersion });

    /// <summary>Puts a page back to what it was (the seeded draft) so tests don't leak into each other.</summary>
    private async Task RestoreAsync(HttpClient admin, string slug, AdminPageBody original)
    {
        var now = await PageAsync(admin, slug);
        var response = await PutAsync(admin, slug, original.Title, original.Body, now.RowVersion);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    // ---- seeds ----

    [Fact]
    public async Task AllFivePages_AreSeeded_AsClearlyMarkedPersianDrafts()
    {
        var admin = await LoginAdminAsync();
        var list = await GetOkAsync<AdminPageListBody[]>(admin, "/api/v1/admin/content-pages");

        Assert.Equal(Slugs, list.Select(p => p.Slug).ToArray());
        foreach (var slug in Slugs)
        {
            var page = await PageAsync(admin, slug);
            Assert.True(page.IsDraft, slug);
            Assert.StartsWith("> **" + ContentPage.DraftMarker + "**", page.Body);
            Assert.Contains("[", page.Body); // placeholders for the owner to fill in
            Assert.Matches("[؀-ۿ]", page.Title); // Persian
        }
    }

    [Fact]
    public async Task ReturnsPage_StatesTheSevenDayRule_ShippingCosts_AndHowToRequest()
    {
        var page = await GetOkAsync<PageBody>(NewClient(), "/api/v1/catalog/pages/returns");

        Assert.Contains("۷ روز", page.Body);
        Assert.Contains("استفاده‌نشده", page.Body);
        Assert.Contains("بسته‌بندی اصلی", page.Body);
        Assert.Contains("هزینهٔ ارسال", page.Body);
        Assert.Contains("نحوهٔ درخواست مرجوعی", page.Body);
    }

    [Fact]
    public async Task Seeding_OnlyInsertsMissingPages_AndNeverOverwritesEdits()
    {
        var admin = await LoginAdminAsync();
        var original = await PageAsync(admin, "terms");
        try
        {
            var edited = "شرایط ویرایش‌شده";
            Assert.True((await PutAsync(admin, "terms", edited, "متن واقعی صفحه", original.RowVersion)).IsSuccessStatusCode);
            using (var scope = Fixture.Factory.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(CancellationToken.None);
            }

            var after = await PageAsync(admin, "terms");
            Assert.Equal(edited, after.Title);
            Assert.False(after.IsDraft);

            // A deleted page is re-created as a draft on the next start; the others stay untouched.
            await Fixture.WithDbAsync(async db => await db.ContentPages.Where(p => p.Slug == "privacy").ExecuteDeleteAsync());
            using (var scope = Fixture.Factory.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(CancellationToken.None);
            }

            Assert.True((await PageAsync(admin, "privacy")).IsDraft);
            Assert.Equal(edited, (await PageAsync(admin, "terms")).Title);
        }
        finally
        {
            await RestoreAsync(admin, "terms", original);
        }
    }

    // ---- public read ----

    [Fact]
    public async Task PublicPages_AreReadableWithoutLogin_ForTheFixedSlugsOnly()
    {
        var client = NewClient();
        var list = await GetOkAsync<PageSummaryBody[]>(client, "/api/v1/catalog/pages");
        Assert.Equal(Slugs, list.Select(p => p.Slug).ToArray());
        Assert.All(list, p => Assert.True(p.UpdatedAtUtc > DateTime.UtcNow.AddYears(-1)));

        var about = await GetOkAsync<PageBody>(client, "/api/v1/catalog/pages/about");
        Assert.False(string.IsNullOrWhiteSpace(about.Body));

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/catalog/pages/nope")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/catalog/pages/..%2Fabout")).StatusCode);
    }

    // ---- security ----

    [Fact]
    public async Task AdminEndpoints_RequireLogin_TheContentPermission_AndCsrf()
    {
        var admin = await LoginAdminAsync();
        var page = await PageAsync(admin, "about");
        var url = "/api/v1/admin/content-pages/about";

        Assert.Equal(HttpStatusCode.Unauthorized, (await NewClient().GetAsync("/api/v1/admin/content-pages")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PutAsync(NewClient(), "about", "x", "y", page.RowVersion)).StatusCode);

        var (customer, _, _) = await RegisterCustomerAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/v1/admin/content-pages")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PutAsync(customer, "about", "x", "y", page.RowVersion)).StatusCode);

        // Content.Manage is separate: a user with only the catalog permission can't edit pages.
        var email = TestData.NewEmail();
        var password = TestData.NewPassword();
        using (var scope = Fixture.Factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true, CreatedAtUtc = DateTime.UtcNow };
            Assert.True((await users.CreateAsync(user, password)).Succeeded);
            Assert.True((await users.AddClaimAsync(user, new Claim("permission", "Catalog.Manage"))).Succeeded);
        }

        var catalogOnly = NewClient();
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(catalogOnly, "/api/v1/auth/login", new { email, password })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await catalogOnly.GetAsync("/api/v1/admin/content-pages")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PutAsync(catalogOnly, "about", "x", "y", page.RowVersion)).StatusCode);

        // No antiforgery header: refused, nothing changed.
        var noCsrf = await admin.PutAsJsonAsync(url, new { title = "x", body = "y", rowVersion = page.RowVersion }, Json);
        await AssertProblemAsync(noCsrf, 400, "antiforgery_invalid");
        Assert.Equal(page.Title, (await PageAsync(admin, "about")).Title);
    }

    // ---- editing ----

    [Fact]
    public async Task Update_SavesTitleAndBody_RecordsWhoAndWhen_AndTheSameVersionIsNotReusable()
    {
        var admin = await LoginAdminAsync();
        var original = await PageAsync(admin, "contact");
        try
        {
            var saved = await SendOkAsync<AdminPageBody>(admin, HttpMethod.Put, "/api/v1/admin/content-pages/contact",
                new { title = "تماس با ما (نهایی)", body = "## تماس\n\nتلفن: ۰۲۱-۱۲۳۴۵۶۷۸", rowVersion = original.RowVersion });

            Assert.Equal("تماس با ما (نهایی)", saved.Title);
            Assert.False(saved.IsDraft); // the marker is gone
            Assert.Equal(ApiFixture.AdminEmail, saved.UpdatedByEmail);
            Assert.NotEqual(original.RowVersion, saved.RowVersion);
            var publicPage = await GetOkAsync<PageBody>(NewClient(), "/api/v1/catalog/pages/contact");
            Assert.Equal("تماس با ما (نهایی)", publicPage.Title);
            Assert.Contains("۰۲۱-۱۲۳۴۵۶۷۸", publicPage.Body);

            var audit = await GetOkAsync<ContentAuditBody[]>(admin, "/api/v1/admin/content-pages/contact/audit");
            Assert.True(audit[0].TitleChanged && audit[0].BodyChanged);
            Assert.Equal(ApiFixture.AdminEmail, audit[0].AdminEmail);

            // saving the old version again is a 409 and changes nothing
            var stale = await PutAsync(admin, "contact", "x2", "y2", original.RowVersion);
            await AssertProblemAsync(stale, 409, "concurrent_update");
            Assert.Equal("تماس با ما (نهایی)", (await PageAsync(admin, "contact")).Title);
        }
        finally
        {
            await RestoreAsync(admin, "contact", original);
        }
    }

    [Fact]
    public async Task TwoEditors_TheSecondSaveGets409_AndNothingIsOverwritten()
    {
        var first = await LoginAdminAsync();
        var second = await LoginAdminAsync();
        var original = await PageAsync(first, "privacy");
        var loadedBySecond = await PageAsync(second, "privacy");
        try
        {
            Assert.True((await PutAsync(first, "privacy", "حریم خصوصی ۱", "متن اول", original.RowVersion)).IsSuccessStatusCode);

            var response = await PutAsync(second, "privacy", "حریم خصوصی ۲", "متن دوم", loadedBySecond.RowVersion);

            await AssertProblemAsync(response, 409, "concurrent_update");
            var now = await PageAsync(first, "privacy");
            Assert.Equal("حریم خصوصی ۱", now.Title);
            Assert.Equal("متن اول", now.Body);
        }
        finally
        {
            await RestoreAsync(first, "privacy", original);
        }
    }

    [Fact]
    public async Task Update_ValidatesTitleAndBody_AndUnknownPagesAre404()
    {
        var admin = await LoginAdminAsync();
        var page = await PageAsync(admin, "about");

        var empty = await PutAsync(admin, "about", "", "", page.RowVersion);
        var problem = await AssertProblemAsync(empty, 400, "validation_failed");
        Assert.True(problem.Errors!.ContainsKey("title"));
        Assert.True(problem.Errors.ContainsKey("body"));

        var tooLong = await PutAsync(admin, "about", "عنوان", new string('ا', ContentPage.BodyMaxLength + 1), page.RowVersion);
        await AssertProblemAsync(tooLong, 400, "validation_failed");

        Assert.Equal(HttpStatusCode.NotFound, (await PutAsync(admin, "pricing", "x۱", "y", page.RowVersion)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/v1/admin/content-pages/pricing")).StatusCode);
        Assert.Equal(page.Title, (await PageAsync(admin, "about")).Title);
    }

    [Fact]
    public async Task SavingWithoutChanges_WritesNoAuditEntry()
    {
        var admin = await LoginAdminAsync();
        var page = await PageAsync(admin, "returns");
        var before = (await GetOkAsync<ContentAuditBody[]>(admin, "/api/v1/admin/content-pages/returns/audit")).Length;

        Assert.True((await PutAsync(admin, "returns", page.Title, page.Body, page.RowVersion)).IsSuccessStatusCode);

        Assert.Equal(before, (await GetOkAsync<ContentAuditBody[]>(admin, "/api/v1/admin/content-pages/returns/audit")).Length);
    }
}
