using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Shop.Infrastructure.Identity;

namespace Shop.IntegrationTests;

public sealed record BlogSummaryBody(string Slug, string Title, string Excerpt, DateTime PublishedAtUtc);

public sealed record BlogListBody(BlogSummaryBody[] Items, int Page, int PageSize, int Total, int TotalPages);

public sealed record BlogPublicBody(string Slug, string Title, string Excerpt, string Body, DateTime PublishedAtUtc);

public sealed record BlogSitemapBody(string Slug, DateTime UpdatedAtUtc);

public sealed record AdminBlogBody(
    int Id, string Slug, string Title, string Excerpt, string Body, bool IsPublished, DateTime? PublishedAtUtc, string? UpdatedByUsername, byte[] RowVersion);

public sealed record AdminBlogListBody(int Id, string Slug, string Title, bool IsPublished);

public sealed record BlogAuditBody(string Action, string? AdminUsername);

public sealed class BlogTests(ApiFixture fixture) : CatalogTestBase(fixture)
{
    private const string Admin = "/api/v1/admin/blog";

    private static string NewSlug() => "t" + Guid.NewGuid().ToString("N")[..12];

    private static Task<HttpResponseMessage> CreateAsync(HttpClient admin, string slug, bool publish = false, string title = "راهنمای خرید لامپ", string excerpt = "خلاصهٔ کوتاه مقاله", string body = "## تیتر\n\nمتن مقاله") =>
        SendAsync(admin, HttpMethod.Post, Admin, new { slug, title, excerpt, body, publish });

    private static async Task<AdminBlogBody> CreateOkAsync(HttpClient admin, string slug, bool publish = false) =>
        await SendOkAsync<AdminBlogBody>(admin, HttpMethod.Post, Admin, new { slug, title = "راهنمای خرید لامپ", excerpt = "خلاصهٔ کوتاه مقاله", body = "## تیتر\n\nمتن مقاله", publish });

    private static Task<HttpResponseMessage> UpdateAsync(HttpClient admin, AdminBlogBody post, bool publish, string? title = null, byte[]? rowVersion = null) =>
        SendAsync(admin, HttpMethod.Put, $"{Admin}/{post.Id}", new { title = title ?? post.Title, excerpt = post.Excerpt, body = post.Body, publish, rowVersion = rowVersion ?? post.RowVersion });

    // ---- public visibility ----

    [Fact]
    public async Task Draft_IsHidden_UntilPublished_AndHiddenAgainWhenUnpublished()
    {
        var admin = await LoginAdminAsync();
        var client = NewClient();
        var slug = NewSlug();
        var post = await CreateOkAsync(admin, slug);
        try
        {
            Assert.False(post.IsPublished);
            Assert.Null(post.PublishedAtUtc);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/catalog/blog/{slug}")).StatusCode);
            Assert.DoesNotContain(slug, (await GetOkAsync<BlogListBody>(client, "/api/v1/catalog/blog")).Items.Select(i => i.Slug));
            Assert.DoesNotContain(slug, (await GetOkAsync<BlogSitemapBody[]>(client, "/api/v1/catalog/blog/sitemap")).Select(i => i.Slug));

            var published = await SendOkAsync<AdminBlogBody>(admin, HttpMethod.Put, $"{Admin}/{post.Id}",
                new { title = post.Title, excerpt = post.Excerpt, body = post.Body, publish = true, rowVersion = post.RowVersion });
            Assert.True(published.IsPublished);
            Assert.NotNull(published.PublishedAtUtc);

            var detail = await GetOkAsync<BlogPublicBody>(client, $"/api/v1/catalog/blog/{slug}");
            Assert.Equal("## تیتر\n\nمتن مقاله", detail.Body);
            var list = await GetOkAsync<BlogListBody>(client, "/api/v1/catalog/blog");
            Assert.Equal(slug, list.Items[0].Slug); // newest first
            Assert.Contains(slug, (await GetOkAsync<BlogSitemapBody[]>(client, "/api/v1/catalog/blog/sitemap")).Select(i => i.Slug));

            var unpublished = await SendOkAsync<AdminBlogBody>(admin, HttpMethod.Put, $"{Admin}/{post.Id}",
                new { title = post.Title, excerpt = post.Excerpt, body = post.Body, publish = false, rowVersion = published.RowVersion });
            Assert.False(unpublished.IsPublished);
            Assert.Equal(published.PublishedAtUtc, unpublished.PublishedAtUtc); // the first publication date is kept
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/catalog/blog/{slug}")).StatusCode);
        }
        finally
        {
            await SendAsync(admin, HttpMethod.Delete, $"{Admin}/{post.Id}");
        }
    }

    [Fact]
    public async Task PublicList_IsPaged_AndAPagePastTheEndIsEmpty()
    {
        var admin = await LoginAdminAsync();
        var client = NewClient();
        var created = new List<AdminBlogBody>();
        try
        {
            for (var i = 0; i < 3; i++)
            {
                created.Add(await CreateOkAsync(admin, NewSlug(), publish: true));
            }

            var first = await GetOkAsync<BlogListBody>(client, "/api/v1/catalog/blog");
            Assert.Equal(1, first.Page);
            Assert.Equal(12, first.PageSize);
            Assert.True(first.Total >= 3);
            Assert.Equal((first.Total + 11) / 12, first.TotalPages);
            Assert.True(first.Items.Length <= 12);
            Assert.Equal(first.Items.OrderByDescending(i => i.PublishedAtUtc).Select(i => i.Slug), first.Items.Select(i => i.Slug));

            var beyond = await GetOkAsync<BlogListBody>(client, $"/api/v1/catalog/blog?page={first.TotalPages + 1}");
            Assert.Empty(beyond.Items);
            Assert.Equal(first.Total, beyond.Total);

            Assert.Equal(1, (await GetOkAsync<BlogListBody>(client, "/api/v1/catalog/blog?page=0")).Page); // clamped
        }
        finally
        {
            foreach (var post in created)
            {
                await SendAsync(admin, HttpMethod.Delete, $"{Admin}/{post.Id}");
            }
        }
    }

    // ---- security ----

    [Fact]
    public async Task AdminEndpoints_RequireLogin_TheBlogPermission_AndCsrf()
    {
        var admin = await LoginAdminAsync();
        var post = await CreateOkAsync(admin, NewSlug());
        try
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await NewClient().GetAsync(Admin)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await CreateAsync(NewClient(), NewSlug())).StatusCode);

            var (customer, _, _) = await RegisterCustomerAsync();
            Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync(Admin)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await CreateAsync(customer, NewSlug())).StatusCode);

            // Blog.Manage is separate: a user with only the catalog permission can't use the blog admin.
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
            Assert.Equal(HttpStatusCode.OK, (await PostAsync(catalogOnly, "/api/v1/auth/login", new { username = email, password })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await catalogOnly.GetAsync(Admin)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await UpdateAsync(catalogOnly, post, true)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(catalogOnly, HttpMethod.Delete, $"{Admin}/{post.Id}")).StatusCode);

            // No antiforgery header: refused, nothing changed.
            var noCsrf = await admin.DeleteAsync($"{Admin}/{post.Id}");
            await AssertProblemAsync(noCsrf, 400, "antiforgery_invalid");
            Assert.Equal(post.Slug, (await GetOkAsync<AdminBlogBody>(admin, $"{Admin}/{post.Id}")).Slug);
        }
        finally
        {
            await SendAsync(admin, HttpMethod.Delete, $"{Admin}/{post.Id}");
        }
    }

    // ---- validation ----

    [Fact]
    public async Task Create_ValidatesEveryField_AndRefusesADuplicateSlug()
    {
        var admin = await LoginAdminAsync();
        var bad = await CreateAsync(admin, "Bad Slug", title: "", excerpt: "", body: "");
        var problem = await AssertProblemAsync(bad, 400, "validation_failed");
        Assert.All(new[] { "slug", "title", "excerpt", "body" }, key => Assert.True(problem.Errors!.ContainsKey(key), key));
        Assert.All(problem.Errors!.Values.SelectMany(v => v), m => Assert.DoesNotContain(m, c => char.IsAsciiLetter(c))); // Persian only

        var slug = NewSlug();
        var post = await CreateOkAsync(admin, slug);
        try
        {
            await AssertProblemAsync(await CreateAsync(admin, slug), 409, "slug_taken");
        }
        finally
        {
            await SendAsync(admin, HttpMethod.Delete, $"{Admin}/{post.Id}");
        }
    }

    // ---- editing ----

    [Fact]
    public async Task Update_RecordsWhoAndWhat_AndAStaleVersionIs409()
    {
        var admin = await LoginAdminAsync();
        var post = await CreateOkAsync(admin, NewSlug());
        try
        {
            Assert.Equal(ApiFixture.AdminEmail, post.UpdatedByUsername);

            var edited = await SendOkAsync<AdminBlogBody>(admin, HttpMethod.Put, $"{Admin}/{post.Id}",
                new { title = "عنوان ویرایش‌شده", excerpt = post.Excerpt, body = post.Body, publish = false, rowVersion = post.RowVersion });
            Assert.Equal("عنوان ویرایش‌شده", edited.Title);
            Assert.Equal(post.Slug, edited.Slug); // the slug never changes

            await AssertProblemAsync(await UpdateAsync(admin, post, false, "دوم"), 409, "concurrent_update"); // old version
            Assert.Equal("عنوان ویرایش‌شده", (await GetOkAsync<AdminBlogBody>(admin, $"{Admin}/{post.Id}")).Title);

            // Even a save with no changes is refused when the version is stale.
            await AssertProblemAsync(await UpdateAsync(admin, edited, false, rowVersion: post.RowVersion), 409, "concurrent_update");

            var audit = await GetOkAsync<BlogAuditBody[]>(admin, $"{Admin}/{post.Id}/audit");
            Assert.Equal(["updated", "created"], audit.Select(a => a.Action).ToArray());
            Assert.All(audit, a => Assert.Equal(ApiFixture.AdminEmail, a.AdminUsername));
        }
        finally
        {
            await SendAsync(admin, HttpMethod.Delete, $"{Admin}/{post.Id}");
        }
    }

    [Fact]
    public async Task TwoEditors_TheSecondSaveGets409_AndNothingIsOverwritten()
    {
        var first = await LoginAdminAsync();
        var second = await LoginAdminAsync();
        var post = await CreateOkAsync(first, NewSlug());
        var loadedBySecond = await GetOkAsync<AdminBlogBody>(second, $"{Admin}/{post.Id}");
        try
        {
            Assert.True((await UpdateAsync(first, post, false, "عنوان اول")).IsSuccessStatusCode);

            await AssertProblemAsync(await UpdateAsync(second, loadedBySecond, false, "عنوان دوم"), 409, "concurrent_update");
            Assert.Equal("عنوان اول", (await GetOkAsync<AdminBlogBody>(first, $"{Admin}/{post.Id}")).Title);
        }
        finally
        {
            await SendAsync(first, HttpMethod.Delete, $"{Admin}/{post.Id}");
        }
    }

    [Fact]
    public async Task Publish_AndUnpublish_AreAudited()
    {
        var admin = await LoginAdminAsync();
        var post = await CreateOkAsync(admin, NewSlug(), publish: true);
        try
        {
            var unpublished = await SendOkAsync<AdminBlogBody>(admin, HttpMethod.Put, $"{Admin}/{post.Id}",
                new { title = post.Title, excerpt = post.Excerpt, body = post.Body, publish = false, rowVersion = post.RowVersion });
            Assert.False(unpublished.IsPublished);

            var audit = await GetOkAsync<BlogAuditBody[]>(admin, $"{Admin}/{post.Id}/audit");
            Assert.Equal("unpublished", audit[0].Action);
            Assert.Contains("published", audit.Select(a => a.Action));
            Assert.Contains("created", audit.Select(a => a.Action));
        }
        finally
        {
            await SendAsync(admin, HttpMethod.Delete, $"{Admin}/{post.Id}");
        }
    }

    [Fact]
    public async Task Delete_RemovesThePost_FromPublicAndAdmin_AndUnknownIdsAre404()
    {
        var admin = await LoginAdminAsync();
        var client = NewClient();
        var post = await CreateOkAsync(admin, NewSlug(), publish: true);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/catalog/blog/{post.Slug}")).StatusCode);

        var deleted = await SendAsync(admin, HttpMethod.Delete, $"{Admin}/{post.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/catalog/blog/{post.Slug}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{Admin}/{post.Id}")).StatusCode);
        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Delete, $"{Admin}/{post.Id}"), 404, "post_not_found");
        Assert.DoesNotContain(post.Id, (await GetOkAsync<AdminBlogListBody[]>(admin, Admin)).Select(p => p.Id));
    }
}
