using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Shop.Domain.Routing;
using Shop.Infrastructure.Identity;
using Shop.Infrastructure.Routing;

namespace Shop.IntegrationTests;

public sealed record ResolveBody(string Kind, int? Id, string? Slug, string? Path, string? To, int? Status);

public sealed record OldPathBody(int Id, string Path);

public sealed record RouteTargetBody(string Type, int Id, string Path, byte[] RowVersion, OldPathBody[] OldPaths);

public sealed record RedirectBody(int Id, string Path, string TargetType, int TargetId, string TargetName, string? CurrentPath, bool IsGone);

public sealed record RoutePageBody<T>(T[] Items, int Page, int PageSize, int Total);

public sealed record RouteAuditBody(string TargetType, int TargetId, string Action, string? OldPath, string? NewPath, string? AdminUsername);

/// <summary>Dynamic routing through the real API: redirects, no chains, conflicts, reserved paths, 410 vs 404, permissions, caching.</summary>
public sealed class RoutesTests(ApiFixture fixture) : StoreTestBase(fixture)
{
    private const string Routes = "/api/v1/admin/routes";

    private static string Slug() => "t" + Guid.NewGuid().ToString("N")[..12];

    private static Task<ResolveBody> ResolveAsync(HttpClient client, string path) =>
        GetOkAsync<ResolveBody>(client, "/api/v1/catalog/resolve?path=" + Uri.EscapeDataString(path));

    private static Task<HttpResponseMessage> ChangeAsync(HttpClient admin, string type, int id, string path, byte[]? rowVersion = null) =>
        SendAsync(admin, HttpMethod.Put, $"{Routes}/{type}/{id}", new { path, rowVersion });

    private static Task<RouteTargetBody> ChangeOkAsync(HttpClient admin, string type, int id, string path) =>
        SendOkAsync<RouteTargetBody>(admin, HttpMethod.Put, $"{Routes}/{type}/{id}", new { path });

    private static Task<RouteTargetBody> TargetAsync(HttpClient admin, string type, int id) => GetOkAsync<RouteTargetBody>(admin, $"{Routes}/{type}/{id}");

    private async Task<(int CategoryId, string CategorySlug)> NewCategoryAsync(HttpClient admin)
    {
        var slug = Slug();
        return ((await CreateCategoryAsync(admin, slug)).Id, slug);
    }

    private async Task<(AdminProductBody Product, string Slug)> NewProductAsync(HttpClient admin, string? path = null)
    {
        var (categoryId, _) = await NewCategoryAsync(admin);
        var slug = Slug();
        var product = await SendOkAsync<AdminProductBody>(admin, HttpMethod.Post, "/api/v1/admin/products", new
        {
            categoryId, name = "محصول " + slug, slug, description = "توضیح", status = "published", path,
            variants = new[] { new { sku = slug + "-0", price = 100_000L, compareAtPrice = (long?)null, stock = 3, isActive = true, sortOrder = 0 } },
        });
        return (product, slug);
    }

    private async Task<HttpClient> LoginWithAsync(params string[] permissions)
    {
        var email = TestData.NewEmail();
        var password = TestData.NewPassword();
        using (var scope = Fixture.Factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true, CreatedAtUtc = DateTime.UtcNow };
            Assert.True((await users.CreateAsync(user, password)).Succeeded);
            foreach (var permission in permissions)
            {
                Assert.True((await users.AddClaimAsync(user, new Claim("permission", permission))).Succeeded);
            }
        }

        var client = NewClient();
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(client, "/api/v1/auth/login", new { username = email, password })).StatusCode);
        return client;
    }

    // ---- defaults: nothing moves --------------------------------------------------------------------------------------

    [Fact]
    public async Task NewEntities_KeepTheUrlTheyAlwaysHad_AndTheResolveEndpointFindsThem()
    {
        var admin = await LoginAdminAsync();
        var anon = NewClient();
        var (categoryId, categorySlug) = await NewCategoryAsync(admin);
        var (product, productSlug) = await NewProductAsync(admin);
        var postSlug = Slug();
        var post = await SendOkAsync<AdminBlogBody>(admin, HttpMethod.Post, "/api/v1/admin/blog",
            new { slug = postSlug, title = "راهنما", excerpt = "خلاصه", body = "متن", publish = true });

        var category = await ResolveAsync(anon, $"/category/{categorySlug}");
        Assert.Equal(("category", categoryId, categorySlug, $"/category/{categorySlug}"), (category.Kind, category.Id, category.Slug, category.Path));
        var resolvedProduct = await ResolveAsync(anon, $"/product/{productSlug}");
        Assert.Equal(("product", product.Id, productSlug), (resolvedProduct.Kind, resolvedProduct.Id, resolvedProduct.Slug));
        var resolvedPost = await ResolveAsync(anon, $"/blog/{postSlug}");
        Assert.Equal(("blogPost", post.Id, $"/blog/{postSlug}"), (resolvedPost.Kind, resolvedPost.Id, resolvedPost.Path));

        var about = await GetOkAsync<AdminContentBody>(admin, "/api/v1/admin/content-pages/about");
        var page = await ResolveAsync(anon, "/about");
        Assert.Equal(("contentPage", about.Id, "/about"), (page.Kind, page.Id, page.Path));
    }

    [Fact]
    public async Task APathChosenOnCreate_IsTheOnlyAddress_AndTheDefaultOneIsNotRegistered()
    {
        var admin = await LoginAdminAsync();
        var anon = NewClient();
        var custom = $"/lustre/{Slug()}";
        var (product, slug) = await NewProductAsync(admin, custom);

        Assert.Equal(("product", product.Id), ((await ResolveAsync(anon, custom)).Kind, (await ResolveAsync(anon, custom)).Id));
        Assert.Equal("notFound", (await ResolveAsync(anon, $"/product/{slug}")).Kind);
        Assert.Equal(custom, (await TargetAsync(admin, "product", product.Id)).Path);
    }

    [Fact]
    public async Task CreatingWithATakenOrReservedPath_FailsAndCreatesNothing()
    {
        var admin = await LoginAdminAsync();
        var (first, firstSlug) = await NewProductAsync(admin);
        var (categoryId, _) = await NewCategoryAsync(admin);
        var slug = Slug();

        var response = await SendAsync(admin, HttpMethod.Post, "/api/v1/admin/products", new
        {
            categoryId, name = "x", slug, status = "published", path = $"/product/{firstSlug}",
            variants = new[] { new { sku = slug + "-0", price = 1000L, compareAtPrice = (long?)null, stock = 1, isActive = true, sortOrder = 0 } },
        });
        var problem = await AssertProblemAsync(response, 409, "path_taken");
        Assert.Contains("محصول", problem.Title!);

        var reserved = await SendAsync(admin, HttpMethod.Post, "/api/v1/admin/categories", new { name = "x", slug = Slug(), sortOrder = 0, isActive = true, path = "/cart" });
        var reservedProblem = await AssertProblemAsync(reserved, 400, "validation_failed");
        Assert.True(reservedProblem.Errors!.ContainsKey("path"));

        // Nothing was half-created: the slug is still free.
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/catalog/products/{slug}")).StatusCode);
        Assert.Equal(firstSlug, first.Slug);
    }

    // ---- redirects, one hop ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task ChangingAPath_MakesTheOldOneA308ToTheCurrentOne_AndChainsCannotForm()
    {
        var admin = await LoginAdminAsync();
        var anon = NewClient();
        var (product, slug) = await NewProductAsync(admin);
        var a = $"/product/{slug}";
        var b = $"/lustre/{slug}";
        var c = $"/lustre/crystal/{slug}";

        await ChangeOkAsync(admin, "product", product.Id, b);
        var afterFirst = await ResolveAsync(anon, a);
        Assert.Equal(("redirect", b, 308), (afterFirst.Kind, afterFirst.To, afterFirst.Status));

        await ChangeOkAsync(admin, "product", product.Id, c);
        foreach (var old in new[] { a, b })
        {
            var hop = await ResolveAsync(anon, old);
            Assert.Equal(("redirect", c, 308), (hop.Kind, hop.To, hop.Status)); // straight to the CURRENT path, never A → B → C
        }

        var current = await ResolveAsync(anon, c);
        Assert.Equal(("product", product.Id, slug, c), (current.Kind, current.Id, current.Slug, current.Path));
        var target = await TargetAsync(admin, "product", product.Id);
        Assert.Equal(c, target.Path);
        Assert.Equal(new[] { a, b }.Order(), target.OldPaths.Select(o => o.Path).Order());

        // Taking one of its own former paths back: it becomes current again, the others still lead to it in one hop.
        await ChangeOkAsync(admin, "product", product.Id, a);
        Assert.Equal("product", (await ResolveAsync(anon, a)).Kind);
        foreach (var old in new[] { b, c })
        {
            Assert.Equal(("redirect", a), ((await ResolveAsync(anon, old)).Kind, (await ResolveAsync(anon, old)).To));
        }
    }

    [Fact]
    public async Task RedirectsWorkForCategoriesPagesAndPostsToo()
    {
        var admin = await LoginAdminAsync();
        var anon = NewClient();
        var (categoryId, categorySlug) = await NewCategoryAsync(admin);
        var about = await GetOkAsync<AdminContentBody>(admin, "/api/v1/admin/content-pages/about");
        var postSlug = Slug();
        var post = await SendOkAsync<AdminBlogBody>(admin, HttpMethod.Post, "/api/v1/admin/blog",
            new { slug = postSlug, title = "راهنما", excerpt = "خلاصه", body = "متن", publish = true });
        var pageCurrent = (await TargetAsync(admin, "contentPage", about.Id)).Path;
        var newCategory = $"/shop/{categorySlug}";
        var newPost = $"/guides/{postSlug}";
        var newPage = "/about-us-" + Slug();
        try
        {
            await ChangeOkAsync(admin, "category", categoryId, newCategory);
            await ChangeOkAsync(admin, "blogPost", post.Id, newPost);
            await ChangeOkAsync(admin, "contentPage", about.Id, newPage);

            Assert.Equal(newCategory, (await ResolveAsync(anon, $"/category/{categorySlug}")).To);
            Assert.Equal(newPost, (await ResolveAsync(anon, $"/blog/{postSlug}")).To);
            Assert.Equal(newPage, (await ResolveAsync(anon, "/about")).To);
        }
        finally
        {
            await ChangeOkAsync(admin, "contentPage", about.Id, pageCurrent); // other tests use /about
        }
    }

    [Theory]
    [InlineData("/PRODUCT/{slug}")]
    [InlineData("/product/{slug}/")]
    [InlineData("  /product/{slug}")]
    public async Task ACaseOrTrailingSlashVariant_RedirectsToTheCanonicalPath(string template)
    {
        var admin = await LoginAdminAsync();
        var (_, slug) = await NewProductAsync(admin);
        var canonical = $"/product/{slug}";

        var variant = await ResolveAsync(NewClient(), template.Replace("{slug}", slug));
        Assert.Equal(("redirect", canonical, 308), (variant.Kind, variant.To, variant.Status));
        Assert.Equal("product", (await ResolveAsync(NewClient(), canonical)).Kind);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("//product/x")]
    [InlineData("/a//b")]
    [InlineData("/لوستر")]
    [InlineData("/never-existed")]
    [InlineData("/product/%2e%2e")]
    [InlineData("/a/b/c/d/e/f")]
    public async Task AnythingElse_IsNotFound(string path) => Assert.Equal("notFound", (await ResolveAsync(NewClient(), path)).Kind);

    // ---- conflicts, reserved, validation ---------------------------------------------------------------------------------

    [Fact]
    public async Task ACurrentPathOfAnotherEntity_IsRefusedWithAPersianMessageNamingTheOwner()
    {
        var admin = await LoginAdminAsync();
        var (first, firstSlug) = await NewProductAsync(admin);
        var (second, _) = await NewProductAsync(admin);

        var problem = await AssertProblemAsync(await ChangeAsync(admin, "product", second.Id, $"/product/{firstSlug}"), 409, "path_taken");
        Assert.Contains("محصول " + firstSlug, problem.Title!);
        Assert.Equal(first.Id, (await ResolveAsync(NewClient(), $"/product/{firstSlug}")).Id); // nothing moved
    }

    [Fact]
    public async Task AnOldPathOfAnotherEntity_IsRefusedUntilItIsReleased()
    {
        var admin = await LoginAdminAsync();
        var anon = NewClient();
        var (first, firstSlug) = await NewProductAsync(admin);
        var (second, _) = await NewProductAsync(admin);
        var old = $"/product/{firstSlug}";
        await ChangeOkAsync(admin, "product", first.Id, $"/lustre/{firstSlug}");

        var problem = await AssertProblemAsync(await ChangeAsync(admin, "product", second.Id, old), 409, "path_taken_redirect");
        Assert.Contains("آزاد", problem.Title!); // tells the admin how to proceed

        var redirect = (await TargetAsync(admin, "product", first.Id)).OldPaths.Single(o => o.Path == old);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(admin, HttpMethod.Delete, $"{Routes}/{redirect.Id}")).StatusCode);
        Assert.Equal("notFound", (await ResolveAsync(anon, old)).Kind);

        await ChangeOkAsync(admin, "product", second.Id, old);
        Assert.Equal(second.Id, (await ResolveAsync(anon, old)).Id);
    }

    [Fact]
    public async Task EveryReservedPath_IsRefused_OnChange_AndOnCreate()
    {
        var admin = await LoginAdminAsync();
        var (product, _) = await NewProductAsync(admin);
        var reserved = ReservedPaths.Segments.Select(s => "/" + s)
            .Concat(ReservedPaths.Exact).Concat(["/cart", "/order/abc123", "/admin/anything", "/api/v1", "/search", "/media/x.jpg", "/checkout", "/BLOG", "/Cart/"]).ToList();

        foreach (var path in reserved)
        {
            var problem = await AssertProblemAsync(await ChangeAsync(admin, "product", product.Id, path), 400, "validation_failed");
            Assert.True(problem.Errors!.ContainsKey("path"), path);
            Assert.Contains(path.Trim('/').ToLowerInvariant() is "blog" ? "نگه داشته" : "", problem.Errors["path"][0]);
        }

        Assert.StartsWith("/product/", (await TargetAsync(admin, "product", product.Id)).Path); // none of them stuck
    }

    [Fact]
    public async Task AReservedWordInsideAPath_IsFine_ButTheBlogIndexAlone_IsNot()
    {
        var admin = await LoginAdminAsync();
        var (product, slug) = await NewProductAsync(admin);

        await ChangeOkAsync(admin, "product", product.Id, $"/blog/{slug}");   // posts live under /blog/…
        await ChangeOkAsync(admin, "product", product.Id, $"/shop/cart-{slug}");
        await AssertProblemAsync(await ChangeAsync(admin, "product", product.Id, "/blog"), 400, "validation_failed");
    }

    [Theory]
    [InlineData("/has space")]
    [InlineData("/under_score")]
    [InlineData("/dot.dot")]
    [InlineData("/-lead")]
    [InlineData("/trail-")]
    [InlineData("/a//b")]
    [InlineData("/لوستر")]
    [InlineData("/a/b/c/d/e")]
    [InlineData("")]
    public async Task InvalidPaths_AreRefused_WithAPersianFieldMessage(string path)
    {
        var admin = await LoginAdminAsync();
        var (product, _) = await NewProductAsync(admin);

        var problem = await AssertProblemAsync(await ChangeAsync(admin, "product", product.Id, path), 400, "validation_failed");
        var message = problem.Errors!["path"].Single();
        Assert.Contains(message, c => c is >= '؀' and <= 'ۿ');
        Assert.DoesNotContain("api.validation", message); // translated, never a key
    }

    [Fact]
    public async Task APathThatIsTooLong_IsRefused()
    {
        var admin = await LoginAdminAsync();
        var (product, _) = await NewProductAsync(admin);

        var problem = await AssertProblemAsync(await ChangeAsync(admin, "product", product.Id, "/" + new string('a', RoutePath.MaxLength)), 400, "validation_failed");
        Assert.Contains("200", problem.Errors!["path"][0]);
    }

    [Fact]
    public async Task UpperCaseAndTrailingSlashTyped_AreNormalizedBeforeSaving()
    {
        var admin = await LoginAdminAsync();
        var (product, slug) = await NewProductAsync(admin);

        var saved = await ChangeOkAsync(admin, "product", product.Id, $"  /Shop/{slug.ToUpperInvariant()}/ ");
        Assert.Equal($"/shop/{slug}", saved.Path);
    }

    [Fact]
    public async Task SettingTheCurrentPathAgain_ChangesNothing()
    {
        var admin = await LoginAdminAsync();
        var (product, slug) = await NewProductAsync(admin);
        var before = await TargetAsync(admin, "product", product.Id);

        var again = await ChangeOkAsync(admin, "product", product.Id, $"/product/{slug}");

        Assert.Equal(before.RowVersion, again.RowVersion);
        Assert.Empty(again.OldPaths);
    }

    [Fact]
    public async Task AStaleVersion_Is409_AndNothingChanges()
    {
        var admin = await LoginAdminAsync();
        var (product, slug) = await NewProductAsync(admin);
        var loaded = await TargetAsync(admin, "product", product.Id);
        await ChangeOkAsync(admin, "product", product.Id, $"/lustre/{slug}");

        await AssertProblemAsync(await ChangeAsync(admin, "product", product.Id, $"/other/{slug}", loaded.RowVersion), 409, "concurrent_update");
        Assert.Equal($"/lustre/{slug}", (await TargetAsync(admin, "product", product.Id)).Path);
    }

    [Fact]
    public async Task AtMost30OldPathsAreKeptPerEntity()
    {
        var admin = await LoginAdminAsync();
        var (product, slug) = await NewProductAsync(admin);
        for (var i = 1; i <= RouteEntry.MaxOldPathsPerTarget; i++)
        {
            await ChangeOkAsync(admin, "product", product.Id, $"/p{i}/{slug}");
        }

        var refused = await ChangeAsync(admin, "product", product.Id, $"/one-more/{slug}");
        var problem = await AssertProblemAsync(refused, 409, "path_limit");
        Assert.Contains("30", problem.Title!);
        Assert.Equal(RouteEntry.MaxOldPathsPerTarget, (await TargetAsync(admin, "product", product.Id)).OldPaths.Length);
    }

    // ---- 410 vs 404 -------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task AHardDeletedPostOrCategory_Is410ForEveryPathItEverHad_AndAnUnknownPathIs404()
    {
        var admin = await LoginAdminAsync();
        var anon = NewClient();
        var slug = Slug();
        var post = await SendOkAsync<AdminBlogBody>(admin, HttpMethod.Post, "/api/v1/admin/blog",
            new { slug, title = "راهنما", excerpt = "خلاصه", body = "متن", publish = true });
        await ChangeOkAsync(admin, "blogPost", post.Id, $"/guides/{slug}");
        var (categoryId, categorySlug) = await NewCategoryAsync(admin);

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(admin, HttpMethod.Delete, $"/api/v1/admin/blog/{post.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(admin, HttpMethod.Delete, $"/api/v1/admin/categories/{categoryId}")).StatusCode);

        Assert.Equal("gone", (await ResolveAsync(anon, $"/blog/{slug}")).Kind);       // the old path
        Assert.Equal("gone", (await ResolveAsync(anon, $"/guides/{slug}")).Kind);     // the current path
        Assert.Equal("gone", (await ResolveAsync(anon, $"/category/{categorySlug}")).Kind);
        Assert.Equal("notFound", (await ResolveAsync(anon, $"/guides/never-{slug}")).Kind);

        // A tombstone keeps the path from being silently reused …
        var (other, _) = await NewProductAsync(admin);
        await AssertProblemAsync(await ChangeAsync(admin, "product", other.Id, $"/guides/{slug}"), 409, "path_taken_gone");

        // … until someone with Routes.Manage releases it.
        var gone = (await GetOkAsync<RoutePageBody<RedirectBody>>(admin, $"{Routes}/redirects?q={slug}")).Items.Where(r => r.IsGone).ToList();
        Assert.Equal(2, gone.Count);
        foreach (var row in gone)
        {
            Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(admin, HttpMethod.Delete, $"{Routes}/{row.Id}")).StatusCode);
        }

        Assert.Equal("notFound", (await ResolveAsync(anon, $"/guides/{slug}")).Kind);
        await ChangeOkAsync(admin, "product", other.Id, $"/guides/{slug}");
    }

    [Fact]
    public async Task AnUnpublishedOrInactiveEntity_StillResolves_SoThePageCanAnswer404ItselfAndKeepItsRedirects()
    {
        var admin = await LoginAdminAsync();
        var anon = NewClient();
        var slug = Slug();
        var draft = await SendOkAsync<AdminBlogBody>(admin, HttpMethod.Post, "/api/v1/admin/blog",
            new { slug, title = "راهنما", excerpt = "خلاصه", body = "متن", publish = false });

        Assert.Equal("blogPost", (await ResolveAsync(anon, $"/blog/{slug}")).Kind);
        Assert.Equal(HttpStatusCode.NotFound, (await anon.GetAsync($"/api/v1/catalog/blog/{slug}")).StatusCode); // the entity API says 404
        Assert.Equal(slug, draft.Slug);
    }

    // ---- cache ------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task AChangeIsVisibleToResolveAtOnce_AndAChangeFromAnotherInstanceAfterTheNextCheck()
    {
        var admin = await LoginAdminAsync();
        var anon = NewClient();
        var (product, slug) = await NewProductAsync(admin);
        Assert.Equal("product", (await ResolveAsync(anon, $"/product/{slug}")).Kind);

        await ChangeOkAsync(admin, "product", product.Id, $"/lustre/{slug}");
        Assert.Equal("redirect", (await ResolveAsync(anon, $"/product/{slug}")).Kind); // no waiting, no restart

        // Another instance writes straight to the database; this instance's table is rebuilt by the periodic check.
        var other = $"/from-elsewhere/{slug}";
        await Fixture.WithDbAsync(async db =>
        {
            var current = db.RoutePaths.Single(r => r.TargetType == "product" && r.TargetId == product.Id && r.IsCurrent);
            current.SetCurrent(false, DateTime.UtcNow);
            await db.SaveChangesAsync();
            db.RoutePaths.Add(RouteEntry.Create(other, "product", product.Id, true, null, DateTime.UtcNow));
            await db.SaveChangesAsync();
            return 0;
        });
        Assert.Equal("product", (await ResolveAsync(anon, $"/lustre/{slug}")).Kind); // stale until the check runs

        await Fixture.Factory.Services.GetRequiredService<RouteTable>().RefreshIfChangedAsync();
        Assert.Equal("redirect", (await ResolveAsync(anon, $"/lustre/{slug}")).Kind);
        Assert.Equal(other, (await ResolveAsync(anon, $"/product/{slug}")).To);
        Assert.Equal("product", (await ResolveAsync(anon, other)).Kind);
    }

    // ---- every public DTO carries the current path ------------------------------------------------------------------------

    [Fact]
    public async Task EveryPublicDto_CarriesTheCurrentPath_AndFollowsAChange()
    {
        var admin = await LoginAdminAsync();
        var anon = NewClient();
        var categorySlug = Slug();
        var category = await CreateCategoryAsync(admin, categorySlug);
        var (product, productSlug) = await NewProductInAsync(admin, category.Id);
        var postSlug = Slug();
        var post = await SendOkAsync<AdminBlogBody>(admin, HttpMethod.Post, "/api/v1/admin/blog",
            new { slug = postSlug, title = "راهنما", excerpt = "خلاصه", body = "متن", publish = true });

        async Task AssertPathsAsync(string categoryPath, string productPath, string postPath)
        {
            Assert.Equal(categoryPath, (await GetOkAsync<JsonElement>(anon, $"/api/v1/catalog/categories/{categorySlug}")).GetProperty("path").GetString());
            Assert.Contains((await GetOkAsync<JsonElement>(anon, "/api/v1/catalog/categories")).EnumerateArray(), c => c.GetProperty("slug").GetString() == categorySlug && c.GetProperty("path").GetString() == categoryPath);
            var detail = await GetOkAsync<JsonElement>(anon, $"/api/v1/catalog/products/{productSlug}");
            Assert.Equal(productPath, detail.GetProperty("path").GetString());
            Assert.Equal(categoryPath, detail.GetProperty("breadcrumb").EnumerateArray().Last().GetProperty("path").GetString());
            var card = (await GetOkAsync<JsonElement>(anon, $"/api/v1/catalog/products?category={categorySlug}")).GetProperty("items").EnumerateArray().Single();
            Assert.Equal(productPath, card.GetProperty("path").GetString());
            var sitemap = await GetOkAsync<JsonElement>(anon, "/api/v1/catalog/sitemap");
            Assert.Contains(sitemap.GetProperty("products").EnumerateArray(), e => e.GetProperty("path").GetString() == productPath);
            Assert.Contains(sitemap.GetProperty("categories").EnumerateArray(), e => e.GetProperty("path").GetString() == categoryPath);
            Assert.Equal(postPath, (await GetOkAsync<JsonElement>(anon, $"/api/v1/catalog/blog/{postSlug}")).GetProperty("path").GetString());
            Assert.Contains((await GetOkAsync<JsonElement>(anon, "/api/v1/catalog/blog")).GetProperty("items").EnumerateArray(), e => e.GetProperty("path").GetString() == postPath);
            Assert.Contains((await GetOkAsync<JsonElement>(anon, "/api/v1/catalog/blog/sitemap")).EnumerateArray(), e => e.GetProperty("path").GetString() == postPath);
        }

        await AssertPathsAsync($"/category/{categorySlug}", $"/product/{productSlug}", $"/blog/{postSlug}");

        await ChangeOkAsync(admin, "category", category.Id, $"/shop/{categorySlug}");
        await ChangeOkAsync(admin, "product", product.Id, $"/shop/{categorySlug}/{productSlug}");
        await ChangeOkAsync(admin, "blogPost", post.Id, $"/guides/{postSlug}");
        await AssertPathsAsync($"/shop/{categorySlug}", $"/shop/{categorySlug}/{productSlug}", $"/guides/{postSlug}");

        var pages = await GetOkAsync<JsonElement>(anon, "/api/v1/catalog/pages");
        Assert.Contains(pages.EnumerateArray(), p => p.GetProperty("slug").GetString() == "about" && p.GetProperty("path").GetString() == "/about");
        Assert.Equal("/about", (await GetOkAsync<JsonElement>(anon, "/api/v1/catalog/pages/about")).GetProperty("path").GetString());
    }

    [Fact]
    public async Task ACartLine_LinksToTheProductsCurrentPath()
    {
        var admin = await LoginAdminAsync();
        var (product, slug) = await NewProductAsync(admin);
        var shopper = NewShopper();
        Assert.True((await shopper.AddAsync(product.Variants[0].Id, 1)).IsSuccessStatusCode);
        Assert.Equal($"/product/{slug}", (await shopper.CartAsync()).Lines.Single().ProductPath);

        await ChangeOkAsync(admin, "product", product.Id, $"/lustre/{slug}");

        Assert.Equal($"/lustre/{slug}", (await shopper.CartAsync()).Lines.Single().ProductPath);
    }

    private async Task<(AdminProductBody Product, string Slug)> NewProductInAsync(HttpClient admin, int categoryId)
    {
        var slug = Slug();
        var product = await SendOkAsync<AdminProductBody>(admin, HttpMethod.Post, "/api/v1/admin/products", new
        {
            categoryId, name = "محصول " + slug, slug, description = "توضیح", status = "published",
            variants = new[] { new { sku = slug + "-0", price = 100_000L, compareAtPrice = (long?)null, stock = 3, isActive = true, sortOrder = 0 } },
        });
        return (product, slug);
    }

    // ---- permissions, redirects page, audit ---------------------------------------------------------------------------------

    [Fact]
    public async Task EntityPermissionsEditPaths_AndOnlyRoutesManageReleasesOrListsRedirects()
    {
        var admin = await LoginAdminAsync();
        var (product, slug) = await NewProductAsync(admin);
        var postSlug = Slug();
        var post = await SendOkAsync<AdminBlogBody>(admin, HttpMethod.Post, "/api/v1/admin/blog",
            new { slug = postSlug, title = "راهنما", excerpt = "خلاصه", body = "متن", publish = true });

        var catalogOnly = await LoginWithAsync("Catalog.Manage");
        Assert.Equal($"/product/{slug}", (await TargetAsync(catalogOnly, "product", product.Id)).Path);
        await ChangeOkAsync(catalogOnly, "product", product.Id, $"/lustre/{slug}");
        Assert.Equal(HttpStatusCode.Forbidden, (await catalogOnly.GetAsync($"{Routes}/blogPost/{post.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ChangeAsync(catalogOnly, "blogPost", post.Id, "/guides/x")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await catalogOnly.GetAsync($"{Routes}/redirects")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await catalogOnly.GetAsync($"{Routes}/audit")).StatusCode);
        var old = (await TargetAsync(admin, "product", product.Id)).OldPaths.Single();
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAsync(catalogOnly, HttpMethod.Delete, $"{Routes}/{old.Id}")).StatusCode);

        var routesOnly = await LoginWithAsync("Routes.Manage");
        Assert.Equal(HttpStatusCode.Forbidden, (await ChangeAsync(routesOnly, "product", product.Id, $"/x/{slug}")).StatusCode); // cannot edit entity paths
        Assert.Equal(HttpStatusCode.Forbidden, (await routesOnly.GetAsync($"{Routes}/product/{product.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await routesOnly.GetAsync($"{Routes}/redirects")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await routesOnly.GetAsync($"{Routes}/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(routesOnly, HttpMethod.Delete, $"{Routes}/{old.Id}")).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await NewClient().GetAsync($"{Routes}/product/{product.Id}")).StatusCode);
        var (customer, _, _) = await RegisterCustomerAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync($"{Routes}/product/{product.Id}")).StatusCode);
        await AssertProblemAsync(await admin.PutAsync($"{Routes}/product/{product.Id}", JsonContent.Create(new { path = "/no-csrf" })), 400, "antiforgery_invalid");
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{Routes}/unicorn/1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"{Routes}/product/999999999")).StatusCode);
    }

    [Fact]
    public async Task TheRedirectsPage_ListsSearchesReleasesAndAudits()
    {
        var admin = await LoginAdminAsync();
        var (product, slug) = await NewProductAsync(admin);
        await ChangeOkAsync(admin, "product", product.Id, $"/lustre/{slug}");

        var list = await GetOkAsync<RoutePageBody<RedirectBody>>(admin, $"{Routes}/redirects?q={slug}");
        var row = Assert.Single(list.Items);
        Assert.Equal(($"/product/{slug}", $"/lustre/{slug}", "product", product.Id, false), (row.Path, row.CurrentPath, row.TargetType, row.TargetId, row.IsGone));
        Assert.Contains(slug, row.TargetName);
        Assert.Single((await GetOkAsync<RoutePageBody<RedirectBody>>(admin, $"{Routes}/redirects?q=محصول {slug}")).Items); // by name
        Assert.Empty((await GetOkAsync<RoutePageBody<RedirectBody>>(admin, $"{Routes}/redirects?q=zzz-{slug}")).Items);

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(admin, HttpMethod.Delete, $"{Routes}/{row.Id}")).StatusCode);
        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Delete, $"{Routes}/{row.Id}"), 404, "route_not_found");
        Assert.Equal("notFound", (await ResolveAsync(NewClient(), $"/product/{slug}")).Kind);

        // The current path cannot be "released".
        var current = await Fixture.WithDbAsync(db => Task.FromResult(db.RoutePaths.Single(r => r.TargetType == "product" && r.TargetId == product.Id && r.IsCurrent).Id));
        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Delete, $"{Routes}/{current}"), 409, "route_is_current");

        var audit = (await GetOkAsync<RoutePageBody<RouteAuditBody>>(admin, $"{Routes}/audit")).Items.Where(a => a.TargetType == "product" && a.TargetId == product.Id).ToList();
        Assert.Contains(audit, a => a.Action == "released" && a.OldPath == $"/product/{slug}" && a.AdminUsername == ApiFixture.AdminEmail);
        Assert.Contains(audit, a => a.Action == "changed" && a.OldPath == $"/product/{slug}" && a.NewPath == $"/lustre/{slug}");
        Assert.Contains(audit, a => a.Action == "created" && a.NewPath == $"/product/{slug}");
    }
}

public sealed record AdminContentBody(int Id, string Slug);
