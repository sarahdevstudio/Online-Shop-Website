using System.Net;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shop.Domain.Blog;
using Shop.Domain.Content;
using Shop.Domain.Routing;
using Shop.Domain.Site;
using Shop.Infrastructure.Catalog;
using Shop.Infrastructure.Persistence;
using Shop.Infrastructure.Routing;
using Shop.Infrastructure.Site;

namespace Shop.IntegrationTests;

public sealed record LinkTargetBody(string Type, int Id, string Label, string? Path, bool Exists, string State);

public sealed record SiteKindLinksBody(string Kind, SitePublishedBody? Published, SiteDraftBody? Draft, Dictionary<string, LinkTargetBody> Links);

/// <summary>Menu, footer and other links that point to an entity: they follow the entity's path with no redirect hop, hide when it is deleted, survive draft/publish/restore, and old typed links are converted once.</summary>
public sealed class EntityLinkTests(ApiFixture fixture) : StoreTestBase(fixture)
{
    private const string Site = "/api/v1/admin/site";

    private static string Slug() => "e" + Guid.NewGuid().ToString("N")[..12];

    private static object Bar() => new { enabled = false, text = "", style = "primary", dismissible = true };

    private static object Chrome(object[] menu, object[]? footerColumns = null) => new
    {
        announcement = Bar(),
        headerMenu = menu,
        footer = new { about = "", columns = footerColumns ?? [] },
        social = new { },
    };

    private static Task<HttpResponseMessage> Save(HttpClient admin, object payload, byte[]? rowVersion = null) =>
        SendAsync(admin, HttpMethod.Put, $"{Site}/chrome/draft", new { payload, rowVersion });

    private static async Task<SiteKindLinksBody> SaveOkAsync(HttpClient admin, object payload)
    {
        var current = await GetOkAsync<SiteKindLinksBody>(admin, $"{Site}/chrome");
        return await SendOkAsync<SiteKindLinksBody>(admin, HttpMethod.Put, $"{Site}/chrome/draft", new { payload, rowVersion = current.Draft?.RowVersion });
    }

    private static async Task PublishAsync(HttpClient admin)
    {
        var current = await GetOkAsync<SiteKindLinksBody>(admin, $"{Site}/chrome");
        await SendOkAsync<SiteKindLinksBody>(admin, HttpMethod.Post, $"{Site}/chrome/publish", new { rowVersion = current.Draft!.RowVersion });
    }

    private static async Task<JsonElement> PublicChromeAsync(HttpClient client) =>
        (await GetOkAsync<JsonElement>(client, "/api/v1/catalog/site")).GetProperty("chrome");

    private static string[] MenuLinks(JsonElement chrome) =>
        chrome.GetProperty("headerMenu").EnumerateArray().Select(m => m.GetProperty("link").ValueKind == JsonValueKind.Null ? string.Empty : m.GetProperty("link").GetString()!).ToArray();

    private static string[] FooterLinks(JsonElement chrome) =>
        chrome.GetProperty("footer").GetProperty("columns").EnumerateArray().SelectMany(c => c.GetProperty("links").EnumerateArray()).Select(l => l.GetProperty("link").GetString()!).ToArray();

    private async Task<(int Id, string Slug)> NewCategoryAsync(HttpClient admin)
    {
        var slug = Slug();
        return ((await CreateCategoryAsync(admin, slug)).Id, slug);
    }

    private static Task<HttpResponseMessage> ChangePathAsync(HttpClient admin, string type, int id, string path) =>
        SendAsync(admin, HttpMethod.Put, $"/api/v1/admin/routes/{type}/{id}", new { path });

    /// <summary>Leaves the published chrome as the seed made it, so other tests see the defaults.</summary>
    private static async Task RestoreFirstVersionAsync(HttpClient admin)
    {
        var current = await GetOkAsync<SiteKindLinksBody>(admin, $"{Site}/chrome");
        if (current.Draft is { } draft)
        {
            await SendOkAsync<SiteKindLinksBody>(admin, HttpMethod.Post, $"{Site}/chrome/discard", new { rowVersion = draft.RowVersion });
        }

        await SendOkAsync<SiteKindLinksBody>(admin, HttpMethod.Post, $"{Site}/chrome/restore/1", new { publish = true });
    }

    [Fact]
    public async Task AnEntityLink_ShowsTheCurrentPath_AfterAPathChange_WithNoRedirectHop_AndAHandTypedLinkDoesNot()
    {
        var admin = await LoginAdminAsync();
        var anon = NewClient();
        var (categoryId, slug) = await NewCategoryAsync(admin);
        var (secondId, secondSlug) = await NewCategoryAsync(admin);
        var typed = $"/category/{secondSlug}";
        try
        {
            await SaveOkAsync(admin, Chrome(
                [new { label = "دسته", entity = new { type = "category", id = categoryId } }, new { label = "دستهٔ دوم", link = typed }],
                [new { title = "ستون", links = new object[] { new { label = "دسته", entity = new { type = "category", id = categoryId } } } }]));
            await PublishAsync(admin);

            var before = await PublicChromeAsync(anon);
            Assert.Equal([$"/category/{slug}", typed], MenuLinks(before));
            Assert.Equal([$"/category/{slug}"], FooterLinks(before));

            Assert.Equal(HttpStatusCode.OK, (await ChangePathAsync(admin, "category", categoryId, $"/shop/{slug}")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await ChangePathAsync(admin, "category", secondId, $"/shop/{secondSlug}")).StatusCode);

            var after = await PublicChromeAsync(anon);
            Assert.Equal([$"/shop/{slug}", typed], MenuLinks(after)); // the entity link is the new path itself; the typed one still points at the old path (a 308 hop)
            Assert.Equal([$"/shop/{slug}"], FooterLinks(after));
            Assert.Equal(308, (await ResolveAsync(anon, typed)).Status);
            Assert.Equal("category", (await ResolveAsync(anon, $"/shop/{slug}")).Kind);
        }
        finally
        {
            await RestoreFirstVersionAsync(admin);
        }
    }

    private static Task<ResolveBody> ResolveAsync(HttpClient client, string path) =>
        GetOkAsync<ResolveBody>(client, "/api/v1/catalog/resolve?path=" + Uri.EscapeDataString(path));

    [Fact]
    public async Task ADeletedEntity_HidesTheLink_AndTheEditorIsToldWhichOnesAreDead_AndAnOldVersionStillRestores()
    {
        var admin = await LoginAdminAsync();
        var anon = NewClient();
        var (keepId, keepSlug) = await NewCategoryAsync(admin);
        var (goneId, _) = await NewCategoryAsync(admin);
        try
        {
            await SaveOkAsync(admin, Chrome(
                [new { label = "می‌ماند", entity = new { type = "category", id = keepId } }, new { label = "می‌رود", entity = new { type = "category", id = goneId } }],
                [new { title = "ستون", links = new object[] { new { label = "می‌رود", entity = new { type = "category", id = goneId } } } }]));
            await PublishAsync(admin);
            Assert.Equal(2, MenuLinks(await PublicChromeAsync(anon)).Length);
            var withBoth = (await GetOkAsync<SiteKindLinksBody>(admin, $"{Site}/chrome")).Published!.Version;

            Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(admin, HttpMethod.Delete, $"/api/v1/admin/categories/{goneId}")).StatusCode);

            var chrome = await PublicChromeAsync(anon);
            Assert.Equal([$"/category/{keepSlug}"], MenuLinks(chrome)); // the dead link is hidden, nothing else changes
            Assert.Empty(chrome.GetProperty("footer").GetProperty("columns").EnumerateArray()); // a column whose links are all dead disappears

            var editor = await GetOkAsync<SiteKindLinksBody>(admin, $"{Site}/chrome");
            Assert.True(editor.Links[$"category:{keepId}"].Exists);
            Assert.False(editor.Links[$"category:{goneId}"].Exists);
            Assert.Equal($"/category/{keepSlug}", editor.Links[$"category:{keepId}"].Path);

            // Saving the same document again (the dead reference included) is fine, and so is restoring a version that holds it.
            var restored = await SendOkAsync<SiteKindLinksBody>(admin, HttpMethod.Post, $"{Site}/chrome/restore/{withBoth}", new { publish = false });
            Assert.NotNull(restored.Draft);
            await PublishAsync(admin);
            Assert.Equal([$"/category/{keepSlug}"], MenuLinks(await PublicChromeAsync(anon)));
        }
        finally
        {
            await RestoreFirstVersionAsync(admin);
        }
    }

    [Fact]
    public async Task APreviewShowsTheDraftsEntityLinks_WhileThePublicDocumentDoesNot()
    {
        var admin = await LoginAdminAsync();
        var anon = NewClient();
        var (id, slug) = await NewCategoryAsync(admin);
        try
        {
            await SaveOkAsync(admin, Chrome([new { label = "فقط-پیش‌نویس-" + slug, entity = new { type = "category", id } }]));

            var draft = (await GetOkAsync<JsonElement>(admin, $"{Site}/compiled?state=draft")).GetProperty("chrome");
            Assert.Equal([$"/category/{slug}"], MenuLinks(draft));
            Assert.DoesNotContain("فقط-پیش‌نویس-" + slug, (await PublicChromeAsync(anon)).GetRawText());
        }
        finally
        {
            await RestoreFirstVersionAsync(admin);
        }
    }

    [Theory]
    [InlineData("nothing")]
    [InlineData("")]
    public async Task AnUnknownEntityType_IsRefusedWithAPersianMessageOnTheLinkField(string type)
    {
        var admin = await LoginAdminAsync();
        var response = await Save(admin, Chrome([new { label = "x", entity = new { type, id = 1 } }]));

        var problem = await AssertProblemAsync(response, 400);
        Assert.Contains("headerMenu[0].link", problem.ValidationErrors!.Keys);
        Assert.Contains("دسته‌بندی", problem.ValidationErrors["headerMenu[0].link"][0]);
    }

    [Fact]
    public async Task TheEntityWins_WhenBothATypedUrlAndAnEntityAreSent_AndATypedUrlStillValidates()
    {
        var admin = await LoginAdminAsync();
        var (id, _) = await NewCategoryAsync(admin);
        try
        {
            var saved = await SaveOkAsync(admin, Chrome([new { label = "x", link = "/anything", entity = new { type = "category", id } }]));
            var item = saved.Draft!.Payload.GetProperty("headerMenu")[0];
            Assert.Equal(JsonValueKind.Null, item.GetProperty("link").ValueKind);
            Assert.Equal(id, item.GetProperty("entity").GetProperty("id").GetInt32());

            await AssertProblemAsync(await Save(admin, Chrome([new { label = "x", link = "javascript:alert(1)" }]), (await GetOkAsync<SiteKindLinksBody>(admin, $"{Site}/chrome")).Draft!.RowVersion), 400);
        }
        finally
        {
            await RestoreFirstVersionAsync(admin);
        }
    }

    // ---- links to something that is not published are hidden like a deleted target ----

    private async Task AssertHiddenUntilPublishedAsync(HttpClient admin, string type, int id, string expectedPath, string hiddenState, Func<Task> hide, Func<Task> show)
    {
        var anon = NewClient();
        try
        {
            await SaveOkAsync(admin, Chrome(
                [new { label = "هدف", entity = new { type, id, label = "نام ذخیره‌شده" } }],
                [new { title = "ستون", links = new object[] { new { label = "هدف", entity = new { type, id } } } }]));
            await PublishAsync(admin);

            Assert.Equal([expectedPath], MenuLinks(await PublicChromeAsync(anon)));

            await hide();
            var hidden = await PublicChromeAsync(anon);
            Assert.Empty(MenuLinks(hidden)); // an item that led nowhere without its link is gone
            Assert.Empty(hidden.GetProperty("footer").GetProperty("columns").EnumerateArray());

            var editor = await GetOkAsync<SiteKindLinksBody>(admin, $"{Site}/chrome");
            var target = editor.Links[$"{type}:{id}"];
            Assert.True(target.Exists); // not deleted: the editor names it and says why it is hidden
            Assert.Equal(hiddenState, target.State);
            Assert.NotEmpty(target.Label);

            await show();
            var back = await PublicChromeAsync(anon); // nothing was republished: the stored reference simply works again
            Assert.Equal([expectedPath], MenuLinks(back));
            Assert.Equal([expectedPath], FooterLinks(back));
            Assert.Equal("published", (await GetOkAsync<SiteKindLinksBody>(admin, $"{Site}/chrome")).Links[$"{type}:{id}"].State);
        }
        finally
        {
            await RestoreFirstVersionAsync(admin);
        }
    }

    [Fact]
    public async Task ALinkToAProduct_IsHiddenWhileItIsADraftOrArchived_AndComesBackWhenPublished()
    {
        var admin = await LoginAdminAsync();
        var (categoryId, _) = await NewCategoryAsync(admin);
        var slug = Slug();
        var product = await CreateProductAsync(admin, categoryId, slug);

        Task Status(string status) => SendOkAsync<AdminProductBody>(admin, HttpMethod.Put, $"/api/v1/admin/products/{product.Id}", new { categoryId, name = product.Name, slug, status });
        await AssertHiddenUntilPublishedAsync(admin, "product", product.Id, $"/product/{slug}", "draft", () => Status("draft"), () => Status("published"));

        await Status("archived");
        Assert.Equal("archived", (await GetOkAsync<LinkTargetBody[]>(admin, $"{Site}/link-targets?type=product&q={Uri.EscapeDataString(product.Name)}")).Single(t => t.Id == product.Id).State);
    }

    [Fact]
    public async Task ALinkToABlogPost_IsHiddenWhileUnpublished_AndComesBackWhenPublished()
    {
        var admin = await LoginAdminAsync();
        var slug = Slug();
        var post = await SendOkAsync<AdminBlogBody>(admin, HttpMethod.Post, "/api/v1/admin/blog", new { slug, title = "راهنمای مقصد", excerpt = "خلاصه", body = "متن", publish = true });

        async Task Publish(bool publish)
        {
            var current = await GetOkAsync<AdminBlogBody>(admin, $"/api/v1/admin/blog/{post.Id}");
            await SendOkAsync<AdminBlogBody>(admin, HttpMethod.Put, $"/api/v1/admin/blog/{post.Id}", new { title = current.Title, excerpt = current.Excerpt, body = current.Body, publish, rowVersion = current.RowVersion });
        }

        await AssertHiddenUntilPublishedAsync(admin, "blogPost", post.Id, $"/blog/{slug}", "draft", () => Publish(false), () => Publish(true));
    }

    [Fact]
    public async Task ALinkToAnInactiveCategory_IsHidden_AndComesBackWhenActivated()
    {
        var admin = await LoginAdminAsync();
        var (id, slug) = await NewCategoryAsync(admin);

        Task Active(bool isActive) => SendOkAsync<IdBody>(admin, HttpMethod.Put, $"/api/v1/admin/categories/{id}", new { name = "دسته " + slug, parentId = (int?)null, sortOrder = 0, isActive });
        await AssertHiddenUntilPublishedAsync(admin, "category", id, $"/category/{slug}", "inactive", () => Active(false), () => Active(true));
    }

    [Fact]
    public async Task ALinkToAnInfoPageWithTheDraftMarker_IsHidden_AndComesBackWhenTheMarkerIsRemoved()
    {
        var admin = await LoginAdminAsync();
        const string slug = ContentSlugs.Returns;
        var original = await GetOkAsync<AdminPageBody>(admin, $"/api/v1/admin/content-pages/{slug}");
        var target = (await GetOkAsync<LinkTargetBody[]>(admin, $"{Site}/link-targets?type=contentPage&q={Uri.EscapeDataString(original.Title)}")).Single(t => t.Label == original.Title);
        var clean = original.Body.Replace(ContentPage.DraftMarker, "نسخهٔ نهایی", StringComparison.Ordinal);
        var marked = "> **" + ContentPage.DraftMarker + "** " + clean;

        async Task Body(string body)
        {
            var current = await GetOkAsync<AdminPageBody>(admin, $"/api/v1/admin/content-pages/{slug}");
            await SendOkAsync<AdminPageBody>(admin, HttpMethod.Put, $"/api/v1/admin/content-pages/{slug}", new { title = current.Title, body, rowVersion = current.RowVersion });
        }

        try
        {
            await Body(clean); // start from a reviewed page
            await AssertHiddenUntilPublishedAsync(admin, "contentPage", target.Id, target.Path!, "draft", () => Body(marked), () => Body(clean));
        }
        finally
        {
            await Body(original.Body);
        }
    }

    [Fact]
    public async Task TheLabelStoredWithAReference_SurvivesTheTargetsDeletion_AndIsTrimmed()
    {
        var admin = await LoginAdminAsync();
        var (id, _) = await NewCategoryAsync(admin);
        try
        {
            var saved = await SaveOkAsync(admin, Chrome([new { label = "هدف", entity = new { type = "category", id, label = "  لوستر   مدرن  " } }]));
            var entity = saved.Draft!.Payload.GetProperty("headerMenu")[0].GetProperty("entity");
            Assert.Equal("لوستر مدرن", entity.GetProperty("label").GetString());

            Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(admin, HttpMethod.Delete, $"/api/v1/admin/categories/{id}")).StatusCode);

            var editor = await GetOkAsync<SiteKindLinksBody>(admin, $"{Site}/chrome");
            Assert.False(editor.Links[$"category:{id}"].Exists);
            Assert.Equal("deleted", editor.Links[$"category:{id}"].State);
            Assert.Equal("لوستر مدرن", editor.Draft!.Payload.GetProperty("headerMenu")[0].GetProperty("entity").GetProperty("label").GetString()); // still there for the editor to name
        }
        finally
        {
            await RestoreFirstVersionAsync(admin);
        }
    }

    [Fact]
    public async Task TheLinkSearch_FindsEntitiesByName_AndNeedsSiteManage()
    {
        var admin = await LoginAdminAsync();
        var slug = Slug();
        var category = await SendOkAsync<IdBody>(admin, HttpMethod.Post, "/api/v1/admin/categories", new { name = "جستجوی-" + slug, slug, isActive = true, sortOrder = 0 });

        var found = await GetOkAsync<LinkTargetBody[]>(admin, $"{Site}/link-targets?type=category&q={Uri.EscapeDataString("جستجوی-" + slug)}");
        var hit = Assert.Single(found);
        Assert.Equal((category.Id, true, $"/category/{slug}"), (hit.Id, hit.Exists, hit.Path));

        Assert.Empty(await GetOkAsync<LinkTargetBody[]>(admin, $"{Site}/link-targets?type=product&q={Uri.EscapeDataString("جستجوی-" + slug)}"));
        await AssertProblemAsync(await admin.GetAsync($"{Site}/link-targets?type=nothing"), 400);
        await AssertProblemAsync(await NewClient().GetAsync($"{Site}/link-targets?type=category"), 401);
    }

    // ---- the one-time conversion -----------------------------------------------------------------------------------

    [Fact]
    public async Task TheConversion_TurnsTypedLinksThatMatchACurrentOrOldPathIntoEntityLinks_OnceAndKeepsTheOthers()
    {
        var connection = ApiFixture.NewDatabaseConnectionString();
        var options = new DbContextOptionsBuilder<ShopDbContext>().UseSqlServer(connection).Options;
        await using (var setup = new ShopDbContext(options))
        {
            await setup.Database.MigrateAsync();
        }

        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(TimeProvider.System);
            services.AddDbContext<ShopDbContext>(o => o.UseSqlServer(connection));
            services.AddSingleton<RouteTable>();
            services.AddScoped<RouteSeeder>();
            services.AddScoped<SiteLinkConverter>();
            await using var provider = services.BuildServiceProvider();

            int categoryId;
            int postId;
            await using (var db = new ShopDbContext(options))
            {
                db.ContentPages.AddRange(ContentSlugs.All.Select(slug => ContentPage.Create(slug, "عنوان " + slug, "متن", DateTime.UtcNow)));
                var seeder = new CatalogSeeder(db, TimeProvider.System, NullLogger<CatalogSeeder>.Instance);
                await seeder.EnsureAttributesAsync(CancellationToken.None);
                await seeder.SeedDevelopmentDataAsync(CancellationToken.None);
                var post = BlogPost.Create("led-guide", "راهنما", "خلاصه", "متن", true, null, DateTime.UtcNow);
                db.BlogPosts.Add(post);
                await db.SaveChangesAsync();
                categoryId = (await db.Categories.FirstAsync()).Id;
                postId = post.Id;
                var slug = (await db.Categories.FirstAsync()).Slug;

                // A typed menu and footer, written the way every site had them before entity links existed.
                var chrome = new ChromePayload
                {
                    HeaderMenu =
                    [
                        new MenuItem { Label = "خانه", Link = "/" },
                        new MenuItem { Label = "درباره", Link = "/about" },
                        new MenuItem { Label = "دسته", Link = "/category/" + slug, Children = [new MenuChild { Label = "راهنما", Link = "/blog/led-guide" }, new MenuChild { Label = "بیرون", Link = "https://example.com/x" }] },
                        new MenuItem { Label = "با فیلتر", Link = $"/category/{slug}?brand=a" },
                    ],
                    Footer = new FooterContent { Columns = [new FooterColumn { Title = "ستون", Links = [new FooterLink { Label = "تماس", Link = "/contact" }, new FooterLink { Label = "جستجو", Link = "/search" }] }] },
                };
                db.SiteConfigVersions.Add(SiteConfigVersion.NewPublished(SiteKind.Chrome, 1, SiteJson.Serialize(chrome), null, DateTime.UtcNow));
                db.SiteConfigVersions.Add(SiteConfigVersion.NewDraft(SiteKind.Chrome, SiteJson.Serialize(chrome), null, DateTime.UtcNow));
                await db.SaveChangesAsync();
            }

            await using (var scope = provider.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<RouteSeeder>().EnsureAsync(CancellationToken.None);
            }

            int converted;
            await using (var scope = provider.CreateAsyncScope())
            {
                converted = await scope.ServiceProvider.GetRequiredService<SiteLinkConverter>().EnsureAsync(CancellationToken.None);
            }

            // published + draft each hold: /about, the category, the post, /contact (4); "/", "/search", the external URL and the filtered URL stay typed.
            Assert.Equal(8, converted);

            await using (var check = new ShopDbContext(options))
            {
                var published = (ChromePayload)SiteJson.TryDeserialize(SiteKind.Chrome, (await check.SiteConfigVersions.SingleAsync(v => v.Kind == SiteKind.Chrome && v.State == SiteState.Published)).Payload)!;
                Assert.Equal("/", published.HeaderMenu[0].Link);
                Assert.Equal(("contentPage", null), (published.HeaderMenu[1].Entity!.Type, published.HeaderMenu[1].Link));
                Assert.Equal("عنوان about", published.HeaderMenu[1].Entity!.Label); // the name is stored with the reference
                Assert.Equal("راهنما", published.HeaderMenu[2].Children[0].Entity!.Label);
                Assert.Equal((RouteTargetTypes.Category, categoryId), (published.HeaderMenu[2].Entity!.Type, published.HeaderMenu[2].Entity!.Id));
                Assert.Equal((RouteTargetTypes.BlogPost, postId), (published.HeaderMenu[2].Children[0].Entity!.Type, published.HeaderMenu[2].Children[0].Entity!.Id));
                Assert.Equal("https://example.com/x", published.HeaderMenu[2].Children[1].Link);
                Assert.Null(published.HeaderMenu[2].Children[1].Entity);
                Assert.StartsWith("/category/", published.HeaderMenu[3].Link, StringComparison.Ordinal); // a link with a query string is a custom URL
                Assert.Null(published.HeaderMenu[3].Entity);
                Assert.Equal("contentPage", published.Footer.Columns[0].Links[0].Entity!.Type);
                Assert.Equal("/search", published.Footer.Columns[0].Links[1].Link);
                Assert.Equal(1, await check.SiteAuditLogs.CountAsync(a => a.Action == SiteAuditAction.Converted));
            }

            // Once only: a custom link typed later that happens to match a path is left alone.
            await using (var scope = provider.CreateAsyncScope())
            {
                Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<SiteLinkConverter>().EnsureAsync(CancellationToken.None));
            }
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await using var cleanup = new ShopDbContext(options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }
}
