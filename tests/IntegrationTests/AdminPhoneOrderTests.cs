using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shop.Domain.Security;
using Shop.Infrastructure.Identity;

namespace Shop.IntegrationTests;

/// <summary>
/// G3: an admin-created ("phone") order. The draft cart is the same server-side cart a shopper gets
/// (AdminDraftOrderController just reuses ICartService with the token as a query parameter instead of a cookie
/// header), and placing it is the same IOrderService.PlaceAsync the shop checkout uses — pricing, atomic stock
/// decrement, idempotency and discount resolution are not reimplemented, only Source=Admin differs.
/// </summary>
public sealed class AdminPhoneOrderTests(ApiFixture fixture) : StoreTestBase(fixture)
{
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

    private static async Task<CartMutationBody> AddDraftItemAsync(HttpClient admin, string? cartToken, int variantId, int quantity) =>
        await SendOkAsync<CartMutationBody>(admin, HttpMethod.Post, $"/api/v1/admin/orders/draft/items?cartToken={cartToken}", new { variantId, quantity });

    private static async Task<CartBody> DraftAsync(HttpClient admin, string? cartToken) =>
        await GetOkAsync<CartBody>(admin, $"/api/v1/admin/orders/draft?cartToken={cartToken}");

    private static async Task<HttpResponseMessage> PlaceDraftAsync(HttpClient admin, string? cartToken, Guid idempotencyKey, object body)
    {
        var csrf = await CsrfTokenAsync(admin);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/orders?cartToken={cartToken}&idempotencyKey={idempotencyKey}") { Content = JsonContent.Create(body, options: Json) };
        request.Headers.Add(csrf.HeaderName, csrf.Token);
        return await admin.SendAsync(request);
    }

    private sealed record ProductSearchItem(int Id, string Slug);

    private sealed record ProductSearchPage(ProductSearchItem[] Items, int Total);

    [Fact]
    public async Task ProductSearch_AlsoMatchesAVariantSku_ForPickingALineToAdd()
    {
        var admin = await LoginAdminAsync();
        var category = await CreateCategoryAsync(admin, Unique("cat"));
        var slug = Unique("skusearch");
        var product = await CreateProductAsync(admin, category.Id, slug, variants: [(100_000, 5)]);
        var sku = product.Variants[0].Sku;

        var bySku = await GetOkAsync<ProductSearchPage>(admin, $"/api/v1/admin/products?q={sku}");
        Assert.Contains(bySku.Items, i => i.Slug == slug);
    }

    [Fact]
    public async Task PlacesAnOrder_WithLivePricingAndAtomicStockDecrement_SourceIsAdmin()
    {
        var admin = await LoginAdminAsync();
        var variants = await CreateVariantsAsync(admin, (100_000, 5), (250_000, 3));

        var add1 = await AddDraftItemAsync(admin, null, variants[0], 2);
        var token = add1.NewToken;
        await AddDraftItemAsync(admin, token, variants[1], 1);
        var cart = await DraftAsync(admin, token);
        Assert.Equal(450_000, cart.Subtotal);

        var key = Guid.NewGuid();
        var response = await PlaceDraftAsync(admin, token, key, Checkout(NewMobile(), "cashOnDelivery", cart.Total));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var placed = await Shopper.ReadAsync<PlacedBody>(response);
        Assert.False(placed.Replayed);

        Assert.Equal(3, await StockOfAsync(variants[0]));
        Assert.Equal(2, await StockOfAsync(variants[1]));

        var orders = await GetOkAsync<AdminOrderListBody>(admin, $"/api/v1/admin/orders?q={placed.Number}");
        var adminOrder = await AdminOrderAsync(admin, orders.Items.Single().Id);
        Assert.Equal("admin", adminOrder.Source);
        Assert.NotNull(adminOrder.StatusLog.Single().AdminUserId);
    }

    [Fact]
    public async Task BypassesTheUnpaidLimit_ButAGuestOrderOfTheSameMobileStillCounts()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 100_000, 50);
        var mobile = NewMobile();

        // Two unpaid guest orders exhaust the per-mobile limit.
        var shopper = NewShopper();
        await shopper.AddAsync(variant, 1);
        await PlaceOkAsync(shopper, mobile);
        var shopper2 = NewShopper();
        await shopper2.AddAsync(variant, 1);
        await PlaceOkAsync(shopper2, mobile);

        // A third guest order for the same mobile is refused.
        var shopper3 = NewShopper();
        await shopper3.AddAsync(variant, 1);
        var (blocked, _) = await PlaceAsync(shopper3, mobile);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);

        // The admin can still place a phone order for that same mobile.
        var add = await AddDraftItemAsync(admin, null, variant, 1);
        var cart = await DraftAsync(admin, add.NewToken);
        var response = await PlaceDraftAsync(admin, add.NewToken, Guid.NewGuid(), Checkout(mobile, "cashOnDelivery", cart.Total));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task RequiresOrdersCreate_AndCsrf()
    {
        var variant = await CreateVariantAsync(await LoginAdminAsync(), 100_000, 5);
        var noPermission = await LoginWithAsync(Permissions.OrdersView);

        await AssertProblemAsync(await SendAsync(noPermission, HttpMethod.Get, "/api/v1/admin/orders/draft?cartToken="), 403);
        await AssertProblemAsync(
            await SendAsync(noPermission, HttpMethod.Post, "/api/v1/admin/orders/draft/items", new { variantId = variant, quantity = 1 }), 403);

        // CSRF: a POST without the antiforgery header is refused, same as every other admin mutation.
        var withPermission = await LoginWithAsync(Permissions.OrdersCreate);
        using var noCsrf = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/orders/draft/items")
        {
            Content = JsonContent.Create(new { variantId = variant, quantity = 1 }, options: Json),
        };
        var response = await withPermission.SendAsync(noCsrf);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PriceChangedBetweenQuoteAndPlacement_Returns409()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 100_000, 5);
        var add = await AddDraftItemAsync(admin, null, variant, 1);
        var cart = await DraftAsync(admin, add.NewToken);

        await SetPriceAsync(variant, 150_000);

        var response = await PlaceDraftAsync(admin, add.NewToken, Guid.NewGuid(), Checkout(NewMobile(), "cashOnDelivery", cart.Total));
        var problem = await AssertProblemAsync(response, 409, "price_changed");
        Assert.NotNull(problem);
    }

    [Fact]
    public async Task ADiscountCode_AppliesWithItsOwnRules_SamePerMobileLimit()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 200_000, 10);
        var code = UniqueCode("ADM");
        await Fixture.WithDbAsync(async db =>
        {
            db.DiscountCodes.Add(Shop.Domain.Discounts.DiscountCode.Create(
                code, Shop.Domain.Discounts.DiscountType.Percent, 10, null, null, 1, null, true, DateTime.UtcNow));
            await db.SaveChangesAsync();
            return true;
        });

        var add = await AddDraftItemAsync(admin, null, variant, 1);
        await SendOkAsync<CartBody>(admin, HttpMethod.Post, $"/api/v1/admin/orders/draft/discount?cartToken={add.NewToken}", new { code });
        var cart = await DraftAsync(admin, add.NewToken);
        Assert.NotNull(cart.Discount);
        Assert.Equal(20_000, cart.Discount!.Amount);

        var response = await PlaceDraftAsync(admin, add.NewToken, Guid.NewGuid(), Checkout(NewMobile(), "cashOnDelivery", cart.Total));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var placed = await Shopper.ReadAsync<PlacedBody>(response);
        var orders = await GetOkAsync<AdminOrderListBody>(admin, $"/api/v1/admin/orders?q={placed.Number}");
        var adminOrder = await AdminOrderAsync(admin, orders.Items.Single().Id);
        Assert.Equal(code, adminOrder.Order.DiscountCode);
    }
}
