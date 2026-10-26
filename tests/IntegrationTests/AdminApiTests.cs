using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Shop.Api.Commands;
using Shop.Infrastructure.Accounts;
using Shop.Infrastructure.Identity;

namespace Shop.IntegrationTests;

public sealed record DashboardBody(int NewOrders, int TransferReferenceWaiting, int ConfirmedToShip, int OrdersToday, int LowStockVariants, int LowStockThreshold);

public sealed record StockResultBody(int VariantId, int Stock, byte[] RowVersion);

public sealed record StockHistoryBody(int Id, Guid? AdminUserId, string? AdminEmail, int Delta, int StockAfter, string? Reason, string Source);

public sealed class AdminApiTests(ApiFixture fixture) : StoreTestBase(fixture)
{
    // ---- helpers ----

    private async Task<(string Email, string Password)> NewAdminUserAsync()
    {
        var email = TestData.NewEmail();
        var password = TestData.NewPassword();
        using var scope = Fixture.Factory.Services.CreateScope();
        var result = await AdminAccounts.CreateAdminAsync(scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(), email, password, TimeProvider.System);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return (email, password);
    }

    private static string AuthCookie(HttpResponseMessage login)
    {
        var header = login.Headers.GetValues("Set-Cookie").Single(h => h.StartsWith("shop_auth=", StringComparison.Ordinal));
        return header.Split(';')[0]; // "shop_auth=<value>"
    }

    /// <summary>A client with no cookie jar that sends exactly the given (possibly stale, copied) auth cookie.</summary>
    private async Task<HttpStatusCode> MeWithCopiedCookieAsync(string cookie)
    {
        var client = Fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Add("Cookie", cookie);
        return (await client.SendAsync(request)).StatusCode;
    }

    private async Task<(HttpClient Client, string Cookie)> LoginWithCookieAsync(string email, string password)
    {
        var client = NewClient();
        var response = await PostAsync(client, "/api/v1/auth/login", new { username = email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (client, AuthCookie(response));
    }

    private HttpClient HostWith(string key, string value, out WebApplicationFactory<Program> host)
    {
        host = Fixture.Factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { [key] = value })));
        return host.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
    }

    // ---- sessions: logout and password change revoke everything ----

    [Fact]
    public async Task Logout_RevokesEverySessionOfTheUser_NotJustTheBrowser()
    {
        var (email, password) = await NewAdminUserAsync();
        var (first, firstCookie) = await LoginWithCookieAsync(email, password);
        var (_, secondCookie) = await LoginWithCookieAsync(email, password); // another device
        Assert.Equal(HttpStatusCode.OK, await MeWithCopiedCookieAsync(firstCookie));
        Assert.Equal(HttpStatusCode.OK, await MeWithCopiedCookieAsync(secondCookie));

        Assert.Equal(HttpStatusCode.NoContent, (await PostAsync(first, "/api/v1/auth/logout")).StatusCode);

        // A copied cookie no longer works, on this session or any other.
        Assert.Equal(HttpStatusCode.Unauthorized, await MeWithCopiedCookieAsync(firstCookie));
        Assert.Equal(HttpStatusCode.Unauthorized, await MeWithCopiedCookieAsync(secondCookie));
        Assert.Equal(HttpStatusCode.Unauthorized, (await first.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task ChangePassword_RevokesOtherSessions_KeepsTheCallerSignedIn_AndSwitchesThePassword()
    {
        var (email, password) = await NewAdminUserAsync();
        var (caller, callerCookie) = await LoginWithCookieAsync(email, password);
        var (_, otherCookie) = await LoginWithCookieAsync(email, password);
        var newPassword = TestData.NewPassword();

        var response = await PostAsync(caller, "/api/v1/auth/change-password", new { currentPassword = password, newPassword });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, await MeWithCopiedCookieAsync(otherCookie)); // the other device is out
        Assert.Equal(HttpStatusCode.Unauthorized, await MeWithCopiedCookieAsync(callerCookie)); // and the old cookie value too
        Assert.Equal(HttpStatusCode.OK, (await caller.GetAsync("/api/v1/auth/me")).StatusCode); // the caller got a fresh cookie
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostAsync(NewClient(), "/api/v1/auth/login", new { username = email, password })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(NewClient(), "/api/v1/auth/login", new { username = email, password = newPassword })).StatusCode);
    }

    [Fact]
    public async Task ChangePassword_Rules()
    {
        var (email, password) = await NewAdminUserAsync();
        var (client, _) = await LoginWithCookieAsync(email, password);

        // wrong current password
        var wrong = await PostAsync(client, "/api/v1/auth/change-password", new { currentPassword = "not-the-password", newPassword = TestData.NewPassword() });
        var problem = await AssertProblemAsync(wrong, 400, "validation_failed");
        Assert.True(problem.Errors!.ContainsKey("currentPassword"));

        // admins need 12+ characters, not repetitive, not containing the e-mail name, not digits only
        foreach (var weak in new[] { "Short1!", "aaaaaaaaaaaaaaaa", "123456789012345", email.Split('@')[0] + "Xy9!" })
        {
            var rejected = await PostAsync(client, "/api/v1/auth/change-password", new { currentPassword = password, newPassword = weak });
            var weakProblem = await AssertProblemAsync(rejected, 400, "validation_failed");
            Assert.True(weakProblem.Errors!.ContainsKey("newPassword"), weak);
        }

        // sign-in state is intact after all that
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostAsync(NewClient(), "/api/v1/auth/change-password", new { currentPassword = password, newPassword = TestData.NewPassword() })).StatusCode);

        // CSRF token is required
        var noCsrf = await client.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = password, newPassword = TestData.NewPassword() }, Json);
        await AssertProblemAsync(noCsrf, 400, "antiforgery_invalid");
    }

    [Fact]
    public async Task GuessingTheCurrentPassword_InChangePassword_LocksTheAccount()
    {
        var (email, password) = await NewAdminUserAsync();
        var (client, _) = await LoginWithCookieAsync(email, password);

        for (var i = 0; i < 5; i++)
        {
            await PostAsync(client, "/api/v1/auth/change-password", new { currentPassword = "wrong-guess-" + i, newPassword = TestData.NewPassword() });
        }

        // locked out: even the right password is refused, with the same generic answer as any failed login
        var login = await PostAsync(NewClient(), "/api/v1/auth/login", new { username = email, password });
        await AssertProblemAsync(login, 401, "invalid_credentials");
    }

    // ---- login brute force ----

    [Fact]
    public async Task FailedLogins_AreIndistinguishable_AndFiveFailuresLockTheAccount()
    {
        var (email, password) = await NewAdminUserAsync();
        var unknown = await PostAsync(NewClient(), "/api/v1/auth/login", new { username = TestData.NewEmail(), password = "whatever-123456" });
        var wrong = await PostAsync(NewClient(), "/api/v1/auth/login", new { username = email, password = "wrong-password-1" });
        var unknownBody = await AssertProblemAsync(unknown, 401, "invalid_credentials");
        var wrongBody = await AssertProblemAsync(wrong, 401, "invalid_credentials");
        Assert.Equal(unknownBody.Detail, wrongBody.Detail);
        Assert.Equal(unknownBody.Title, wrongBody.Title);

        for (var i = 0; i < 4; i++)
        {
            await PostAsync(NewClient(), "/api/v1/auth/login", new { username = email, password = "wrong-password-" + i });
        }

        // five failures in total: now locked for 15 minutes, and the answer is still the same generic one
        var locked = await PostAsync(NewClient(), "/api/v1/auth/login", new { username = email, password });
        var lockedBody = await AssertProblemAsync(locked, 401, "invalid_credentials");
        Assert.Equal(unknownBody.Detail, lockedBody.Detail);
    }

    [Fact]
    public async Task LoginAttempts_PerAccount_AreRateLimited_WhateverTheEmailIs()
    {
        var client = HostWith("Auth:AccountAttemptsPerMinute", "3", out var host);
        await using var _ = host;
        var email = TestData.NewEmail(); // does not even exist

        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            codes.Add((await PostAsync(client, "/api/v1/auth/login", new { username = email, password = "wrong-password-1" })).StatusCode);
        }

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests], codes);
        // another account is unaffected
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostAsync(client, "/api/v1/auth/login", new { username = TestData.NewEmail(), password = "x-wrong-password" })).StatusCode);
    }

    [Fact]
    public void SessionLength_IsConfigurable_DefaultEightHours()
    {
        var defaultOptions = Fixture.Factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme);
        Assert.Equal(TimeSpan.FromHours(8), defaultOptions.ExpireTimeSpan);
        Assert.True(defaultOptions.SlidingExpiration);

        HostWith("Auth:SessionHours", "2", out var host);
        using (host)
        {
            var custom = host.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme);
            Assert.Equal(TimeSpan.FromHours(2), custom.ExpireTimeSpan);
        }
    }

    // ---- step-up re-authentication ----

    private static readonly string[] Sensitive = ["cardNumber", "cardHolderName", "bankName", "cardToCardEnabled", "cashOnDeliveryEnabled"];

    private async Task<AdminSettingsBody> SettingsAsync(HttpClient admin) => await GetOkAsync<AdminSettingsBody>(admin, "/api/v1/admin/settings");

    private Task<HttpResponseMessage> PutSettingsAsync(HttpClient admin, SettingsValuesBody values, byte[] rowVersion, string? password) =>
        SendAsync(admin, HttpMethod.Put, "/api/v1/admin/settings", new { settings = values, rowVersion, currentPassword = password });

    [Fact]
    public async Task SensitiveSettings_NeedTheCurrentPassword_OthersDoNot()
    {
        var (email, password) = await NewAdminUserAsync();
        var (admin, _) = await LoginWithCookieAsync(email, password);
        var original = await SettingsAsync(admin);
        try
        {
            // not sensitive: no password needed
            var fee = original.Settings with { ShippingFlatFee = original.Settings.ShippingFlatFee + 1 };
            var ok = await PutSettingsAsync(admin, fee, original.RowVersion, null);
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
            var current = await SettingsAsync(admin);

            // each sensitive change: refused without / with a wrong password, accepted with the right one
            var changes = new Dictionary<string, SettingsValuesBody>
            {
                ["cardNumber"] = current.Settings with { CardNumber = "6219861234567898" },
                ["cardHolderName"] = current.Settings with { CardHolderName = "مریم کریمی" },
                ["bankName"] = current.Settings with { BankName = "ملت" },
                ["cardToCardEnabled"] = current.Settings with { CardToCardEnabled = false },
                ["cashOnDeliveryEnabled"] = current.Settings with { CashOnDeliveryEnabled = false },
            };
            foreach (var (field, values) in changes)
            {
                var latest = await SettingsAsync(admin);
                await AssertProblemAsync(await PutSettingsAsync(admin, values, latest.RowVersion, null), 403, "step_up_failed");
                if (field == "cardNumber")
                {
                    // one wrong guess is enough here: every wrong password counts towards the lockout (see the lockout test)
                    await AssertProblemAsync(await PutSettingsAsync(admin, values, latest.RowVersion, "wrong-password-xx"), 403, "step_up_failed");
                }

                Assert.Equal(latest.Settings, (await SettingsAsync(admin)).Settings); // nothing changed for field: refused
                Assert.True(Sensitive.Contains(field));
            }

            var finalCurrent = await SettingsAsync(admin);
            var done = await PutSettingsAsync(admin, changes["cardNumber"] with { ShippingFlatFee = finalCurrent.Settings.ShippingFlatFee }, finalCurrent.RowVersion, password);
            Assert.Equal(HttpStatusCode.OK, done.StatusCode);
        }
        finally
        {
            // put the shared baseline back with the (always correct) seed admin's password
            var seedAdmin = await LoginAdminAsync();
            var now = await SettingsAsync(seedAdmin);
            await Fixture.WithDbAsync(async db => await db.StoreSettings.ExecuteUpdateAsync(s => s
                .SetProperty(x => x.CardNumber, ApiFixture.TestCard).SetProperty(x => x.CardHolderName, "رضا احمدی").SetProperty(x => x.BankName, "ملی")
                .SetProperty(x => x.CardToCardEnabled, true).SetProperty(x => x.CashOnDeliveryEnabled, true).SetProperty(x => x.ShippingFlatFee, 50_000L)));
            Fixture.Factory.Services.GetRequiredService<Shop.Application.Settings.IStoreSettings>().Invalidate();
            Assert.Equal(ApiFixture.TestCard, (await SettingsAsync(seedAdmin)).Settings.CardNumber);
            _ = now;
        }
    }

    [Fact]
    public async Task FailedStepUp_IsAudited_WithFieldNamesOnly()
    {
        var (email, password) = await NewAdminUserAsync();
        var (admin, _) = await LoginWithCookieAsync(email, password);
        var current = await SettingsAsync(admin);
        var secretCard = "6219861234567898";

        await AssertProblemAsync(
            await PutSettingsAsync(admin, current.Settings with { CardNumber = secretCard, BankName = "ملت" }, current.RowVersion, "wrong-password-xx"), 403, "step_up_failed");

        var audit = await GetOkAsync<AuditPageBody>(admin, "/api/v1/admin/settings/audit?page=1");
        var entry = audit.Items.First(e => e.Field == "reauthFailed" && e.AdminEmail == email);
        Assert.Contains("cardNumber", entry.NewValue);
        Assert.Contains("bankName", entry.NewValue);
        Assert.DoesNotContain(secretCard, entry.NewValue);
        Assert.DoesNotContain("ملت", entry.NewValue); // field names only, never the attempted values
        Assert.Null(entry.OldValue);
        Assert.Equal((await SettingsAsync(admin)).Settings, current.Settings);
    }

    [Fact]
    public async Task GuessingThePassword_ThroughTheSettingsForm_LocksTheAccountLikeTheLoginPage()
    {
        var (email, password) = await NewAdminUserAsync();
        var (admin, _) = await LoginWithCookieAsync(email, password);
        var current = await SettingsAsync(admin);
        var change = current.Settings with { BankName = "ملت" };

        for (var i = 0; i < 5; i++)
        {
            await AssertProblemAsync(await PutSettingsAsync(admin, change, current.RowVersion, "guess-number-" + i), 403, "step_up_failed");
        }

        // locked: the correct password is refused too, and so is signing in
        await AssertProblemAsync(await PutSettingsAsync(admin, change, current.RowVersion, password), 403, "step_up_failed");
        await AssertProblemAsync(await PostAsync(NewClient(), "/api/v1/auth/login", new { username = email, password }), 401, "invalid_credentials");
    }

    // ---- dashboard ----

    [Fact]
    public async Task Dashboard_CountsOrders_AndRequiresTheOrdersPermission()
    {
        var admin = await LoginAdminAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await NewClient().GetAsync("/api/v1/admin/dashboard")).StatusCode);
        var (customer, _, _) = await RegisterCustomerAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/v1/admin/dashboard")).StatusCode);

        var before = await GetOkAsync<DashboardBody>(admin, "/api/v1/admin/dashboard");
        var variant = await CreateVariantAsync(admin, 100_000, 2); // 2 ≤ the low-stock threshold
        var shopper = NewShopper();
        await shopper.AddAsync(variant, 1);
        var placed = await PlaceOkAsync(shopper, method: "cardToCard");
        var withOrder = await GetOkAsync<DashboardBody>(admin, "/api/v1/admin/dashboard");
        Assert.Equal(before.NewOrders + 1, withOrder.NewOrders);
        Assert.Equal(before.OrdersToday + 1, withOrder.OrdersToday);
        Assert.Equal(before.TransferReferenceWaiting, withOrder.TransferReferenceWaiting);
        Assert.Equal(before.LowStockVariants + 1, withOrder.LowStockVariants);
        Assert.Equal(3, withOrder.LowStockThreshold);

        await shopper.SendAsync(HttpMethod.Post, $"/api/v1/store/orders/{placed.AccessToken}/transfer-reference",
            new { tracking = "12345678", time = "now", cardLast4 = "1234" });
        Assert.Equal(before.TransferReferenceWaiting + 1, (await GetOkAsync<DashboardBody>(admin, "/api/v1/admin/dashboard")).TransferReferenceWaiting);

        var id = await OrderIdAsync(placed.Number);
        await SendAsync(admin, HttpMethod.Post, $"/api/v1/admin/orders/{id}/confirm-payment");
        var confirmed = await GetOkAsync<DashboardBody>(admin, "/api/v1/admin/dashboard");
        Assert.Equal(before.NewOrders, confirmed.NewOrders);
        Assert.Equal(before.TransferReferenceWaiting, confirmed.TransferReferenceWaiting);
        Assert.Equal(before.ConfirmedToShip + 1, confirmed.ConfirmedToShip);

        await SendAsync(admin, HttpMethod.Post, $"/api/v1/admin/orders/{id}/cancel");
    }

    // ---- stock adjust ----

    [Fact]
    public async Task StockAdjust_ChangesAtomically_NeverBelowZero_AndIsAudited()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 100_000, 5);

        var up = await SendOkAsync<StockResultBody>(admin, HttpMethod.Post, $"/api/v1/admin/variants/{variant}/stock-adjust", new { delta = 7, reason = "ورود کالا" });
        Assert.Equal(12, up.Stock);
        var down = await SendOkAsync<StockResultBody>(admin, HttpMethod.Post, $"/api/v1/admin/variants/{variant}/stock-adjust", new { delta = -12 });
        Assert.Equal(0, down.Stock);
        Assert.NotEqual(up.RowVersion, down.RowVersion);

        var below = await SendAsync(admin, HttpMethod.Post, $"/api/v1/admin/variants/{variant}/stock-adjust", new { delta = -1 });
        await AssertProblemAsync(below, 409, "stock_out_of_range");
        Assert.Equal(0, await StockOfAsync(variant));
        Assert.Equal(HttpStatusCode.BadRequest, (await SendAsync(admin, HttpMethod.Post, $"/api/v1/admin/variants/{variant}/stock-adjust", new { delta = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendAsync(admin, HttpMethod.Post, "/api/v1/admin/variants/2000000000/stock-adjust", new { delta = 1 })).StatusCode);

        var history = await GetOkAsync<StockHistoryBody[]>(admin, $"/api/v1/admin/variants/{variant}/stock-history");
        Assert.Equal(2, history.Length); // the rejected attempts left no trace
        Assert.Equal(new[] { -12, 7 }, history.Select(h => h.Delta).ToArray()); // newest first
        Assert.Equal(new[] { 0, 12 }, history.Select(h => h.StockAfter).ToArray());
        Assert.All(history, h => Assert.Equal(ApiFixture.AdminEmail, h.AdminEmail));
        Assert.Equal("ورود کالا", history[1].Reason);
        Assert.All(history, h => Assert.Equal("adjust", h.Source));
    }

    [Fact]
    public async Task StockAdjust_InParallel_LosesNothing_AndNeverGoesNegative()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 100_000, 5);
        var clients = new List<HttpClient>();
        for (var i = 0; i < 10; i++)
        {
            clients.Add(await LoginAdminAsync());
        }

        var responses = await Task.WhenAll(clients.Select(c => SendAsync(c, HttpMethod.Post, $"/api/v1/admin/variants/{variant}/stock-adjust", new { delta = -1 })));

        Assert.Equal(5, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(5, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(0, await StockOfAsync(variant));
        Assert.Equal(5, (await GetOkAsync<StockHistoryBody[]>(admin, $"/api/v1/admin/variants/{variant}/stock-history")).Length);
    }

    [Fact]
    public async Task StockAdjust_RacingWithCheckout_KeepsEveryUnit()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 100_000, 10);
        var shoppers = Enumerable.Range(0, 4).Select(_ => NewShopper()).ToList();
        foreach (var s in shoppers)
        {
            await s.AddAsync(variant, 1);
        }

        var totals = await Task.WhenAll(shoppers.Select(s => s.CartAsync()));
        var orders = shoppers.Select((s, i) => s.SendAsync(HttpMethod.Post, "/api/v1/store/orders", Checkout(NewMobile(), "cashOnDelivery", totals[i].Total), Guid.NewGuid()));
        var adjusts = Enumerable.Range(0, 4).Select(_ => SendAsync(admin, HttpMethod.Post, $"/api/v1/admin/variants/{variant}/stock-adjust", new { delta = 5 }));

        var results = await Task.WhenAll(orders.Concat(adjusts));

        Assert.All(results, r => Assert.True(r.IsSuccessStatusCode, r.StatusCode.ToString()));
        Assert.Equal(10 + 20 - 4, await StockOfAsync(variant)); // 4 sold, 20 added, nothing lost
    }

    [Fact]
    public async Task StockAdjust_Security_And_VariantEditsAreAuditedToo()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 100_000, 5);
        var url = $"/api/v1/admin/variants/{variant}/stock-adjust";

        Assert.Equal(HttpStatusCode.Unauthorized, (await PostAsync(NewClient(), url, new { delta = 1 })).StatusCode);
        var (customer, _, _) = await RegisterCustomerAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(customer, url, new { delta = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync($"/api/v1/admin/variants/{variant}/stock-history")).StatusCode);
        await AssertProblemAsync(await admin.PostAsJsonAsync(url, new { delta = 1 }, Json), 400, "antiforgery_invalid");
        Assert.Equal(5, await StockOfAsync(variant));

        // the exact-value edit (with the row version) is audited as an "edit"
        var product = await Fixture.WithDbAsync(db => db.ProductVariants.Where(v => v.Id == variant).Select(v => new { v.Sku, v.Price, v.RowVersion, v.SortOrder }).SingleAsync());
        await SendOkAsync<AdminVariantBody>(admin, HttpMethod.Put, $"/api/v1/admin/variants/{variant}",
            new { sku = product.Sku, price = product.Price, compareAtPrice = (long?)null, stock = 9, isActive = true, sortOrder = product.SortOrder, rowVersion = product.RowVersion });

        var history = await GetOkAsync<StockHistoryBody[]>(admin, $"/api/v1/admin/variants/{variant}/stock-history");
        var edit = Assert.Single(history);
        Assert.Equal("edit", edit.Source);
        Assert.Equal(4, edit.Delta);
        Assert.Equal(9, edit.StockAfter);
    }

    // ---- create-admin ----

    [Fact]
    public async Task CreateAdmin_CreatesAnAdminWhoCanSignIn_AndRefusesWeakOrDuplicateAccounts()
    {
        var email = TestData.NewEmail();
        var password = TestData.NewPassword();
        using var scope = Fixture.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        Assert.True((await AdminAccounts.CreateAdminAsync(users, email, password, TimeProvider.System)).IsSuccess);

        var (client, _) = await LoginWithCookieAsync(email, password);
        var me = await GetOkAsync<AccountBody>(client, "/api/v1/auth/me");
        Assert.Contains("Admin", me.Roles);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/admin/settings")).StatusCode);

        Assert.Equal("username_taken", (await AdminAccounts.CreateAdminAsync(users, email, password, TimeProvider.System)).Error!.Code);
        foreach (var weak in new[] { "short-pw-1", "Abcdefghij1", "aaaaaaaaaaaaaaaaaa", "111111111111111", "" })
        {
            var result = await AdminAccounts.CreateAdminAsync(users, TestData.NewEmail(), weak, TimeProvider.System);
            Assert.False(result.IsSuccess, $"'{weak}' should be refused");
        }

        var other = TestData.NewEmail();
        var containsName = await AdminAccounts.CreateAdminAsync(users, other, "x" + other.Split('@')[0] + "9Z!", TimeProvider.System);
        Assert.False(containsName.IsSuccess, "a password containing the e-mail name should be refused");

        foreach (var badName in new[] { "ab", "has space", "bad/name", new string('a', 65) })
        {
            Assert.False((await AdminAccounts.CreateAdminAsync(users, badName, password, TimeProvider.System)).IsSuccess, $"'{badName}' should be refused");
            Assert.Null(await users.FindByNameAsync(badName));
        }

        Assert.False((await AdminAccounts.CreateAdminAsync(users, "valid.name", password, TimeProvider.System, "no-at-sign")).IsSuccess);

        // A plain username with no e-mail at all can sign in.
        var plain = "owner" + Guid.NewGuid().ToString("N")[..8];
        Assert.True((await AdminAccounts.CreateAdminAsync(users, plain, password, TimeProvider.System)).IsSuccess);
        var (plainClient, _) = await LoginWithCookieAsync(plain, password);
        var plainMe = await GetOkAsync<AccountBody>(plainClient, "/api/v1/auth/me");
        Assert.Equal(plain, plainMe.Username);
        Assert.Null(plainMe.Email);
    }

    [Theory]
    [InlineData("--username owner", "owner", null, null)]
    [InlineData("--username=owner", "owner", null, null)]
    [InlineData("--username owner --email a@b.test", "owner", "a@b.test", null)]
    [InlineData("--password hunter2hunter2", null, null, "never accepted")]
    [InlineData("--username owner --password hunter2hunter2", null, null, "never accepted")]
    [InlineData("--username owner -p hunter2hunter2", null, null, "never accepted")]
    [InlineData("--password=hunter2hunter2 --username owner", null, null, "never accepted")]
    [InlineData("", null, null, "--username is required")]
    [InlineData("--username", null, null, "--username is required")]
    [InlineData("--email a@b.test", null, null, "--username is required")]
    [InlineData("--username owner --force", null, null, "Unknown argument")]
    public void CreateAdmin_Arguments_NeverAcceptAPassword(string arguments, string? expectedUsername, string? expectedEmail, string? expectedError)
    {
        var args = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var (username, email, error) = CreateAdminCommand.ParseArguments(args);

        Assert.Equal(expectedUsername, username);
        Assert.Equal(expectedEmail, email);
        if (expectedError is null)
        {
            Assert.Null(error);
        }
        else
        {
            Assert.Contains(expectedError, error);
            Assert.DoesNotContain("hunter2", error); // the refused value is never echoed back
        }
    }
}
