using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shop.Domain.Security;

namespace Shop.IntegrationTests;

public sealed record PublicReviewBody(string Name, int Rating, string Text, DateTime CreatedAtUtc);

public sealed record ReviewSummaryBody(int Count, double Average, PublicReviewBody[] Items);

public sealed record AdminReviewBody(int Id, string ProductSlug, string ProductName, string Name, string Mobile, int Rating, string Text, string Status, string? ModeratedBy, byte[] RowVersion);

public sealed record AdminReviewListBody(AdminReviewBody[] Items, int Total, int Pending, int Approved, int Rejected);

public sealed record ReviewAuditEntryBody(int Id, int ReviewId, string Action, string? AdminUsername, string ProductName);

public sealed record ReviewAuditBody(ReviewAuditEntryBody[] Items, int Total);

/// <summary>Guest reviews (docs/18): submitted pending, public only when approved, one per mobile per product, rate-limited, moderated and audited.</summary>
public sealed class ReviewTests(ApiFixture fixture) : StoreTestBase(fixture)
{
    private const string Text = "کیفیت نور عالی است و بسته‌بندی هم سالم رسید.";

    private static string NewReviewMobile() => "09" + Random.Shared.NextInt64(100_000_000, 999_999_999);

    private async Task<(string Slug, string Name)> NewProductAsync(HttpClient admin, string status = "published")
    {
        var category = await CreateCategoryAsync(admin, Unique("rev-cat"));
        var slug = Unique("rev");
        var product = await CreateProductAsync(admin, category.Id, slug, status: status);
        return (slug, product.Name);
    }

    private static object Review(string slug, string? mobile = null, int rating = 5, string text = Text, string name = "علی رضایی", string? website = null) =>
        new { productSlug = slug, name, mobile = mobile ?? NewReviewMobile(), rating, text, website };

    private Task<HttpResponseMessage> SubmitAsync(object body, string? ip = null)
    {
        var shopper = NewShopper();
        return shopper.SendAsync(HttpMethod.Post, "/api/v1/store/reviews", body);
    }

    private static Task<ReviewSummaryBody> PublicAsync(HttpClient client, string slug) => GetOkAsync<ReviewSummaryBody>(client, $"/api/v1/catalog/reviews/{slug}");

    private async Task<AdminReviewBody> FindAsync(HttpClient admin, string slug, string? mobile = null)
    {
        var list = await GetOkAsync<AdminReviewListBody>(admin, "/api/v1/admin/reviews");
        return list.Items.First(r => r.ProductSlug == slug && (mobile is null || r.Mobile == mobile));
    }

    [Fact]
    public async Task ASubmittedReview_IsPending_NotPublic_UntilApproved_ThenCountsInTheAverage()
    {
        var admin = await LoginAdminAsync();
        var (slug, _) = await NewProductAsync(admin);
        var anon = NewClient();
        Assert.Equal(new ReviewSummaryBody(0, 0, []).Count, (await PublicAsync(anon, slug)).Count);

        var mobile = NewReviewMobile();
        var response = await SubmitAsync(Review(slug, mobile, rating: 4));
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var pending = await PublicAsync(anon, slug);
        Assert.Equal((0, 0.0, 0), (pending.Count, pending.Average, pending.Items.Length)); // held for moderation: nothing public

        var queued = await FindAsync(admin, slug, mobile);
        Assert.Equal(("pending", "علی رضایی", 4), (queued.Status, queued.Name, queued.Rating));
        var approved = await SendOkAsync<AdminReviewBody>(admin, HttpMethod.Post, $"/api/v1/admin/reviews/{queued.Id}/approve", new { rowVersion = queued.RowVersion });
        Assert.Equal("approved", approved.Status);
        Assert.Equal(ApiFixture.AdminEmail, approved.ModeratedBy);

        await SubmitAsync(Review(slug, rating: 5, name: "مریم"));
        var second = (await GetOkAsync<AdminReviewListBody>(admin, "/api/v1/admin/reviews?status=pending")).Items.First(r => r.ProductSlug == slug);
        await SendOkAsync<AdminReviewBody>(admin, HttpMethod.Post, $"/api/v1/admin/reviews/{second.Id}/approve", new { rowVersion = second.RowVersion });

        var summary = await PublicAsync(anon, slug);
        Assert.Equal((2, 4.5), (summary.Count, summary.Average));
        Assert.Equal(["مریم", "علی رضایی"], summary.Items.Select(i => i.Name)); // newest first
        // The public view never carries the mobile number.
        var raw = await (await anon.GetAsync($"/api/v1/catalog/reviews/{slug}")).Content.ReadAsStringAsync();
        Assert.DoesNotContain(mobile, raw);
        Assert.DoesNotContain("mobile", raw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OneReviewPerMobilePerProduct_IncludingRejectedOnes_ButAnotherProductIsFine()
    {
        var admin = await LoginAdminAsync();
        var (slug, _) = await NewProductAsync(admin);
        var (other, _) = await NewProductAsync(admin);
        var mobile = NewReviewMobile();
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(Review(slug, mobile))).StatusCode);
        await AssertProblemAsync(await SubmitAsync(Review(slug, mobile, text: "یک نظر دیگر و تازه از همان شماره.")), 409, "review_exists");
        // The same number in another notation is the same person.
        await AssertProblemAsync(await SubmitAsync(Review(slug, "+98" + mobile[1..])), 409, "review_exists");

        var queued = await FindAsync(admin, slug, mobile);
        await SendOkAsync<AdminReviewBody>(admin, HttpMethod.Post, $"/api/v1/admin/reviews/{queued.Id}/reject", new { rowVersion = queued.RowVersion });
        await AssertProblemAsync(await SubmitAsync(Review(slug, mobile)), 409, "review_exists"); // rejected still uses the number

        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(Review(other, mobile))).StatusCode);
    }

    [Fact]
    public async Task Validation_Rating_Name_Mobile_Text_AndTheProduct()
    {
        var admin = await LoginAdminAsync();
        var (slug, _) = await NewProductAsync(admin);
        var (draft, _) = await NewProductAsync(admin, "draft");

        async Task<Dictionary<string, string[]>> Errors(object body) => (await AssertProblemAsync(await SubmitAsync(body), 400)).Errors!;
        Assert.Contains("rating", (await Errors(Review(slug, rating: 0))).Keys);
        Assert.Contains("rating", (await Errors(Review(slug, rating: 6))).Keys);
        Assert.Contains("name", (await Errors(Review(slug, name: "ع"))).Keys);
        Assert.Contains("name", (await Errors(Review(slug, name: new string('ب', 61)))).Keys);
        Assert.Contains("mobile", (await Errors(Review(slug, "123"))).Keys);
        Assert.Contains("text", (await Errors(Review(slug, text: "کوتاه"))).Keys);
        Assert.Contains("text", (await Errors(Review(slug, text: new string('ب', 2001)))).Keys);

        await AssertProblemAsync(await SubmitAsync(Review("no-such-product")), 404);
        await AssertProblemAsync(await SubmitAsync(Review(draft)), 404); // an unpublished product cannot be reviewed
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(Review(slug, "۰۹۱۲۳۴۵۶۷۸۹", name: "علی", text: new string('ب', 10)))).StatusCode); // Persian digits, boundary lengths
    }

    [Fact]
    public async Task AFilledHoneypot_IsAcceptedSilently_AndNothingIsStored()
    {
        var admin = await LoginAdminAsync();
        var (slug, _) = await NewProductAsync(admin);
        var mobile = NewReviewMobile();
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(Review(slug, mobile, website: "http://spam.example"))).StatusCode);
        var list = await GetOkAsync<AdminReviewListBody>(admin, "/api/v1/admin/reviews");
        Assert.DoesNotContain(list.Items, r => r.Mobile == mobile);
        // ...and the number is not used up by it.
        Assert.Equal(HttpStatusCode.Accepted, (await SubmitAsync(Review(slug, mobile))).StatusCode);
    }

    [Fact]
    public async Task SubmittingIsRateLimitedPerAddress_AndNeedsTheStorefrontKey()
    {
        var admin = await LoginAdminAsync();
        var (slug, _) = await NewProductAsync(admin);
        var shopper = NewShopper(); // one address
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 7; i++)
        {
            statuses.Add((await shopper.SendAsync(HttpMethod.Post, "/api/v1/store/reviews", Review(slug))).StatusCode);
        }

        Assert.Equal(5, statuses.Count(s => s == HttpStatusCode.Accepted));
        Assert.Equal(2, statuses.Count(s => s == HttpStatusCode.TooManyRequests));

        // Without the key (a browser calling the API directly) the endpoint does not exist for the caller.
        var direct = await NewClient().PostAsJsonAsync("/api/v1/store/reviews", Review(slug));
        Assert.True(direct.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound, $"got {(int)direct.StatusCode}");
    }

    [Fact]
    public async Task Moderation_NeedsItsOwnPermission_Csrf_AndHonoursTheRowVersion()
    {
        var admin = await LoginAdminAsync();
        var (slug, _) = await NewProductAsync(admin);
        await SubmitAsync(Review(slug));
        var review = await FindAsync(admin, slug);

        // anonymous and a role without Reviews.Manage
        Assert.Equal(HttpStatusCode.Unauthorized, (await NewClient().GetAsync("/api/v1/admin/reviews")).StatusCode);
        var role = await SendOkAsync<StaffRoleBody>(admin, HttpMethod.Post, "/api/v1/admin/staff/roles",
            new { name = "NoRev" + Guid.NewGuid().ToString("N")[..8], title = "بدون نظرات", permissions = new[] { Permissions.OrdersManage }, currentPassword = Fixture.AdminPassword });
        var username = "norev-" + Guid.NewGuid().ToString("N")[..8];
        var password = TestData.NewPassword() + "-Zz9";
        await SendOkAsync<StaffUserBody>(admin, HttpMethod.Post, "/api/v1/admin/staff", new { username, displayName = "x", email = (string?)null, password, roles = new[] { role.Name }, currentPassword = Fixture.AdminPassword });
        var limited = NewClient();
        await PostAsync(limited, "/api/v1/auth/login", new { username, password });
        Assert.Equal(HttpStatusCode.Forbidden, (await limited.GetAsync("/api/v1/admin/reviews")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(limited, HttpMethod.Post, $"/api/v1/admin/reviews/{review.Id}/approve", new { rowVersion = review.RowVersion })).StatusCode);
        // and without the CSRF token
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/api/v1/admin/reviews/{review.Id}/approve", new { rowVersion = review.RowVersion })).StatusCode);

        // a stale version is a 409; an unknown id a 404
        var approved = await SendOkAsync<AdminReviewBody>(admin, HttpMethod.Post, $"/api/v1/admin/reviews/{review.Id}/approve", new { rowVersion = review.RowVersion });
        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Post, $"/api/v1/admin/reviews/{review.Id}/reject", new { rowVersion = review.RowVersion }), 409, "concurrent_update");
        var rejected = await SendOkAsync<AdminReviewBody>(admin, HttpMethod.Post, $"/api/v1/admin/reviews/{review.Id}/reject", new { rowVersion = approved.RowVersion });
        Assert.Equal("rejected", rejected.Status);
        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Post, "/api/v1/admin/reviews/99999999/approve", new { rowVersion = new byte[8] }), 404);
        Assert.Equal(0, (await PublicAsync(NewClient(), slug)).Count); // taken down again: not public

        // every decision is in the log, with who made it
        var audit = await GetOkAsync<ReviewAuditBody>(admin, "/api/v1/admin/reviews/audit");
        var mine = audit.Items.Where(i => i.ReviewId == review.Id).Select(i => (i.Action, i.AdminUsername)).ToList();
        Assert.Contains(("approved", ApiFixture.AdminEmail), mine);
        Assert.Contains(("rejected", ApiFixture.AdminEmail), mine);
    }

    [Fact]
    public async Task TheQueueFiltersByStatus_AndCountsEachState()
    {
        var admin = await LoginAdminAsync();
        var (slug, _) = await NewProductAsync(admin);
        await SubmitAsync(Review(slug));
        await SubmitAsync(Review(slug));
        var pending = await GetOkAsync<AdminReviewListBody>(admin, "/api/v1/admin/reviews?status=pending");
        Assert.All(pending.Items, r => Assert.Equal("pending", r.Status));
        Assert.True(pending.Pending >= 2);
        var first = pending.Items.First(r => r.ProductSlug == slug);
        await SendOkAsync<AdminReviewBody>(admin, HttpMethod.Post, $"/api/v1/admin/reviews/{first.Id}/approve", new { rowVersion = first.RowVersion });
        var approved = await GetOkAsync<AdminReviewListBody>(admin, "/api/v1/admin/reviews?status=approved");
        Assert.Contains(approved.Items, r => r.Id == first.Id);
        Assert.True(approved.Approved >= 1);
    }
}
