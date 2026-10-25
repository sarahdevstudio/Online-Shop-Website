using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shop.Application.Settings;
using Shop.Infrastructure.Identity;
using Shop.Infrastructure.Persistence;

namespace Shop.IntegrationTests;

public sealed record SettingsValuesBody(
    long ShippingFlatFee, long? FreeShippingThreshold, bool CardToCardEnabled, bool CashOnDeliveryEnabled, string CardNumber,
    string CardHolderName, string BankName, int MaxQuantityPerLine, string StorePhone, string StoreAddress, string WorkingHours,
    string InstagramHandle);

public sealed record AdminSettingsBody(SettingsValuesBody Settings, bool CardToCardOffered, byte[] RowVersion);

public sealed record AuditEntryBody(int Id, Guid? AdminUserId, string? AdminEmail, string Field, string? OldValue, string? NewValue);

public sealed record AuditPageBody(AuditEntryBody[] Items, int Total);

public sealed record StoreInfoBody(
    string StorePhone, string StoreAddress, string WorkingHours, string InstagramHandle, long ShippingFlatFee,
    long? FreeShippingThreshold, bool CardToCard, bool CashOnDelivery);

public sealed class SettingsTests(ApiFixture fixture) : StoreTestBase(fixture)
{
    private static readonly SettingsValuesBody Baseline = new(
        50_000, null, true, true, ApiFixture.TestCard, "رضا احمدی", "ملی", 10, "", "", "", "");

    private const string OtherCard = "6219861234567898"; // Luhn-valid

    private async Task<AdminSettingsBody> CurrentAsync(HttpClient admin) =>
        await GetOkAsync<AdminSettingsBody>(admin, "/api/v1/admin/settings");

    private static Task<HttpResponseMessage> PutAsync(HttpClient admin, SettingsValuesBody values, byte[] rowVersion) =>
        SendAsync(admin, HttpMethod.Put, "/api/v1/admin/settings", new { settings = values, rowVersion });

    /// <summary>Saves a change on top of the current settings and returns the response; always check the status in the test.</summary>
    private async Task<HttpResponseMessage> ChangeAsync(HttpClient admin, Func<SettingsValuesBody, SettingsValuesBody> change)
    {
        var current = await CurrentAsync(admin);
        return await PutAsync(admin, change(current.Settings), current.RowVersion);
    }

    private async Task<AdminSettingsBody> ChangeOkAsync(HttpClient admin, Func<SettingsValuesBody, SettingsValuesBody> change)
    {
        var response = await ChangeAsync(admin, change);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AdminSettingsBody>(ApiJson))!;
    }

    /// <summary>Tests share one database: every test that edits settings puts the baseline back.</summary>
    private async Task RestoreAsync(HttpClient admin)
    {
        // A direct write first: a test may have left a state the API refuses to save (e.g. an unconfigured card).
        await Fixture.WithDbAsync(async db => await db.StoreSettings.ExecuteUpdateAsync(s => s
            .SetProperty(x => x.CardNumber, ApiFixture.TestCard)
            .SetProperty(x => x.CardHolderName, "رضا احمدی")
            .SetProperty(x => x.CardToCardEnabled, true)
            .SetProperty(x => x.CashOnDeliveryEnabled, true)));
        Fixture.Factory.Services.GetRequiredService<IStoreSettings>().Invalidate();
        var response = await ChangeAsync(admin, _ => Baseline);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task<StoreInfoBody> StoreInfoAsync(HttpClient client) =>
        await GetOkAsync<StoreInfoBody>(client, "/api/v1/catalog/store-info");

    private async Task<AuditEntryBody[]> AuditAsync(HttpClient admin) =>
        (await GetOkAsync<AuditPageBody>(admin, "/api/v1/admin/settings/audit?page=1")).Items;

    // ---- security ----

    [Fact]
    public async Task SettingsEndpoints_RequireLogin_AndTheSettingsPermission()
    {
        var anonymous = NewClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/settings/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PutAsync(anonymous, Baseline, [])).StatusCode);

        var (customer, _, _) = await RegisterCustomerAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/v1/admin/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/v1/admin/settings/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PutAsync(customer, Baseline, [])).StatusCode);
    }

    [Fact]
    public async Task CatalogManager_WithoutSettingsManage_IsForbidden()
    {
        // Settings.Manage is separate from Catalog.Manage: a user holding only the catalog permission can't touch settings.
        var email = TestData.NewEmail();
        var password = TestData.NewPassword();
        using (var scope = Fixture.Factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email, EmailConfirmed = true, CreatedAtUtc = DateTime.UtcNow };
            Assert.True((await users.CreateAsync(user, password)).Succeeded);
            Assert.True((await users.AddClaimAsync(user, new Claim("permission", "Catalog.Manage"))).Succeeded);
        }

        var client = NewClient();
        Assert.Equal(HttpStatusCode.OK, (await PostAsync(client, "/api/v1/auth/login", new { email, password })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/admin/products")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/settings/audit")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PutAsync(client, Baseline, [])).StatusCode);
    }

    [Fact]
    public async Task Update_WithoutTheAntiforgeryToken_IsRejected_AndChangesNothing()
    {
        var admin = await LoginAdminAsync();
        var current = await CurrentAsync(admin);

        var response = await admin.PutAsJsonAsync("/api/v1/admin/settings",
            new { settings = current.Settings with { StorePhone = "0210000000" }, rowVersion = current.RowVersion }, Json);

        await AssertProblemAsync(response, 400, "antiforgery_invalid");
        Assert.Equal(current.Settings.StorePhone, (await CurrentAsync(admin)).Settings.StorePhone);
    }

    // ---- reading ----

    [Fact]
    public async Task FirstStart_WritesStoreDefaults_AndLaterStartsDoNotOverwriteEdits()
    {
        var admin = await LoginAdminAsync();
        var seeded = await CurrentAsync(admin);
        Assert.Equal(ApiFixture.TestCard, seeded.Settings.CardNumber); // StoreDefaults__CardNumber from the test host
        Assert.True(seeded.CardToCardOffered);

        try
        {
            await ChangeOkAsync(admin, s => s with { ShippingFlatFee = 77_000 });
            using (var scope = Fixture.Factory.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync(CancellationToken.None);
            }

            Assert.Equal(77_000, (await CurrentAsync(admin)).Settings.ShippingFlatFee); // the defaults (50 000) did not come back
        }
        finally
        {
            await RestoreAsync(admin);
        }
    }

    [Fact]
    public async Task PublicStoreInfo_ShowsOnlyCustomerFacingFields()
    {
        var admin = await LoginAdminAsync();
        try
        {
            await ChangeOkAsync(admin, s => s with
            {
                StorePhone = "۰۲۱-۱۲۳۴۵۶۷۸", StoreAddress = "تهران، خیابان آزادی", WorkingHours = "۹ تا ۱۸", InstagramHandle = "@my.shop",
            });

            var response = await NewClient().GetAsync("/api/v1/catalog/store-info"); // anonymous
            var raw = await response.Content.ReadAsStringAsync();
            var info = System.Text.Json.JsonSerializer.Deserialize<StoreInfoBody>(raw, Json)!;

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("021-12345678", info.StorePhone);
            Assert.Equal("my.shop", info.InstagramHandle);
            Assert.Equal(50_000, info.ShippingFlatFee);
            Assert.True(info.CardToCard);
            Assert.True(info.CashOnDelivery);
            Assert.DoesNotContain(ApiFixture.TestCard, raw);
            Assert.DoesNotContain("رضا احمدی", raw); // holder name is for the order page, not the public info
            Assert.DoesNotContain("rowVersion", raw, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await RestoreAsync(admin);
        }
    }

    // ---- validation through the API ----

    [Fact]
    public async Task Update_RejectsInvalidValues_WithFieldErrors_AndSavesNothing()
    {
        var admin = await LoginAdminAsync();
        var before = await CurrentAsync(admin);

        async Task<ProblemBody> Rejected(Func<SettingsValuesBody, SettingsValuesBody> change, string field)
        {
            var problem = await AssertProblemAsync(await ChangeAsync(admin, change), 400, "validation_failed");
            Assert.True(problem.Errors!.ContainsKey(field), $"{field} not in {string.Join(",", problem.Errors.Keys)}");
            return problem;
        }

        await Rejected(s => s with { CardNumber = "6037991234567890" }, "cardNumber"); // bad Luhn
        await Rejected(s => s with { CardNumber = "123" }, "cardNumber");
        await Rejected(s => s with { CardNumber = "6037000000000000" }, "cardNumber"); // the old sample
        await Rejected(s => s with { CardNumber = "" }, "cardNumber"); // card-to-card stays enabled
        await Rejected(s => s with { CardHolderName = "" }, "cardHolderName");
        await Rejected(s => s with { CardToCardEnabled = false, CashOnDeliveryEnabled = false }, "paymentMethods");
        await Rejected(s => s with { ShippingFlatFee = -1 }, "shippingFlatFee");
        await Rejected(s => s with { FreeShippingThreshold = -1 }, "freeShippingThreshold");
        await Rejected(s => s with { MaxQuantityPerLine = 0 }, "maxQuantityPerLine");
        await Rejected(s => s with { InstagramHandle = "not valid!" }, "instagramHandle");

        var after = await CurrentAsync(admin);
        Assert.Equal(before.Settings, after.Settings);
        Assert.Equal(before.RowVersion, after.RowVersion);
    }

    [Fact]
    public async Task Update_AcceptsPersianDigitCardNumbers_AndStoresTheNormalizedNumber()
    {
        var admin = await LoginAdminAsync();
        try
        {
            var saved = await ChangeOkAsync(admin, s => s with { CardNumber = "۶۲۱۹ ۸۶۱۲ ۳۴۵۶ ۷۸۹۸", BankName = "  ملت " });

            Assert.Equal(OtherCard, saved.Settings.CardNumber);
            Assert.Equal("ملت", saved.Settings.BankName);
        }
        finally
        {
            await RestoreAsync(admin);
        }
    }

    [Fact]
    public async Task CardToCard_CanBeSwitchedOff_ButNotTogetherWithCashOnDelivery()
    {
        var admin = await LoginAdminAsync();
        try
        {
            var off = await ChangeOkAsync(admin, s => s with { CardToCardEnabled = false, CardNumber = "", CardHolderName = "", BankName = "" });
            Assert.False(off.CardToCardOffered);
            await AssertProblemAsync(await ChangeAsync(admin, s => s with { CashOnDeliveryEnabled = false }), 400, "validation_failed");
        }
        finally
        {
            await RestoreAsync(admin);
        }
    }

    [Fact]
    public async Task StaleRowVersion_Returns409_AndNothingIsLost()
    {
        var admin = await LoginAdminAsync();
        var other = await LoginAdminAsync();
        try
        {
            var loaded = await CurrentAsync(admin);
            await ChangeOkAsync(other, s => s with { StorePhone = "0211111111" }); // someone else saves first

            var response = await PutAsync(admin, loaded.Settings with { StoreAddress = "نشانی من" }, loaded.RowVersion);

            await AssertProblemAsync(response, 409, "concurrent_update");
            var now = await CurrentAsync(admin);
            Assert.Equal("0211111111", now.Settings.StorePhone);
            Assert.Equal("", now.Settings.StoreAddress);
        }
        finally
        {
            await RestoreAsync(admin);
        }
    }

    // ---- audit log ----

    [Fact]
    public async Task EveryChangedField_IsLogged_WithWhoOldAndNew_AndNoopSavesAreNot()
    {
        var admin = await LoginAdminAsync();
        var phone = "021" + Random.Shared.Next(10_000_000, 99_999_999);
        try
        {
            await ChangeOkAsync(admin, s => s with { ShippingFlatFee = 61_000, StorePhone = phone });
            var noop = await CurrentAsync(admin);
            var countBefore = (await GetOkAsync<AuditPageBody>(admin, "/api/v1/admin/settings/audit")).Total;
            await PutAsync(admin, noop.Settings, noop.RowVersion); // nothing changed
            Assert.Equal(countBefore, (await GetOkAsync<AuditPageBody>(admin, "/api/v1/admin/settings/audit")).Total);

            var entries = await AuditAsync(admin);
            var fee = entries.First(e => e.Field == "shippingFlatFee" && e.NewValue == "61000");
            var phoneEntry = entries.First(e => e.Field == "storePhone" && e.NewValue == phone);
            Assert.Equal("50000", fee.OldValue);
            Assert.Equal("", phoneEntry.OldValue);
            Assert.Equal(ApiFixture.AdminEmail, fee.AdminEmail);
            Assert.NotNull(fee.AdminUserId);
        }
        finally
        {
            await RestoreAsync(admin);
        }
    }

    [Fact]
    public async Task AuditLog_NeverHoldsAFullCardNumber()
    {
        var admin = await LoginAdminAsync();
        try
        {
            await ChangeOkAsync(admin, s => s with { CardNumber = OtherCard });
            var response = await admin.GetAsync("/api/v1/admin/settings/audit?page=1");
            var raw = await response.Content.ReadAsStringAsync();

            var entry = (await AuditAsync(admin)).First(e => e.Field == "cardNumber" && e.NewValue == "************7898");
            Assert.Equal("************7893", entry.OldValue);
            Assert.DoesNotContain(OtherCard, raw);
            Assert.DoesNotContain(ApiFixture.TestCard, raw);

            // The database itself holds only the masked values too.
            var stored = await Fixture.WithDbAsync(db => db.SettingsAuditLogs.Where(a => a.Field == "cardNumber")
                .Select(a => (a.OldValue ?? "") + "|" + (a.NewValue ?? "")).ToListAsync());
            Assert.All(stored, v => Assert.DoesNotContain("6037991234567893", v));
            Assert.All(stored, v => Assert.DoesNotContain(OtherCard, v));
        }
        finally
        {
            await RestoreAsync(admin);
        }
    }

    // ---- cache ----

    [Fact]
    public async Task Settings_AreCached_AndAnAdminUpdateInvalidatesTheCache()
    {
        var admin = await LoginAdminAsync();
        var client = NewClient();
        var provider = Fixture.Factory.Services.GetRequiredService<IStoreSettings>();
        try
        {
            Assert.Equal("", (await StoreInfoAsync(client)).StorePhone); // loads the cache

            // An out-of-band database edit is not seen while the cache holds the old value...
            await Fixture.WithDbAsync(async db => await db.StoreSettings.ExecuteUpdateAsync(s => s.SetProperty(x => x.StorePhone, "0219999999")));
            Assert.Equal("", (await StoreInfoAsync(client)).StorePhone);

            // ...until the cache is invalidated.
            provider.Invalidate();
            Assert.Equal("0219999999", (await StoreInfoAsync(client)).StorePhone);

            // An admin update is visible immediately, with no manual invalidation.
            await ChangeOkAsync(admin, s => s with { StorePhone = "0218888888" });
            Assert.Equal("0218888888", (await StoreInfoAsync(client)).StorePhone);
        }
        finally
        {
            await RestoreAsync(admin);
        }
    }

    // ---- checkout uses the settings ----

    [Fact]
    public async Task ShippingFee_AndFreeShippingThreshold_ComeFromTheSettings_AndAreSnapshottedOnTheOrder()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 100_000, 20);
        try
        {
            await ChangeOkAsync(admin, s => s with { ShippingFlatFee = 70_000, FreeShippingThreshold = 250_000 });
            var shopper = NewShopper();
            await shopper.AddAsync(variant, 1);
            var cart = await shopper.CartAsync();
            Assert.Equal(70_000, cart.ShippingFee);
            Assert.Equal(250_000, cart.FreeShippingThreshold);
            var placed = await PlaceOkAsync(shopper);

            await shopper.AddAsync(variant, 3); // 300 000 ≥ threshold: free
            var free = await shopper.CartAsync();
            Assert.Equal(0, free.ShippingFee);
            var freePlaced = await PlaceOkAsync(shopper);

            await ChangeOkAsync(admin, s => s with { ShippingFlatFee = 99_000, FreeShippingThreshold = null });
            Assert.Equal(70_000, (await GetOrderAsync(shopper, placed.AccessToken)).ShippingFee); // existing orders keep their own fee
            Assert.Equal(0, (await GetOrderAsync(shopper, freePlaced.AccessToken)).ShippingFee);
        }
        finally
        {
            await RestoreAsync(admin);
        }
    }

    [Fact]
    public async Task MaxQuantityPerLine_ComesFromTheSettings()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 100_000, 50);
        try
        {
            await ChangeOkAsync(admin, s => s with { MaxQuantityPerLine = 3 });
            var shopper = NewShopper();

            var body = await Shopper.ReadAsync<CartMutationBody>(await shopper.AddAsync(variant, 8));

            Assert.True(body.Clamped);
            Assert.Equal(3, body.Cart.Lines.Single().Quantity);
        }
        finally
        {
            await RestoreAsync(admin);
        }
    }

    [Fact]
    public async Task CardDetails_AreSnapshottedOnTheOrder_AndLaterChangesDoNotTouchIt()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 100_000, 20);
        try
        {
            var shopper = NewShopper();
            await shopper.AddAsync(variant, 1);
            var first = await PlaceOkAsync(shopper, method: "cardToCard");
            var before = (await GetOrderAsync(shopper, first.AccessToken)).PaymentInstructions!;
            Assert.Equal(ApiFixture.TestCard, before.CardNumber);
            Assert.Equal("رضا احمدی", before.HolderName);
            Assert.Equal("ملی", before.BankName);
            Assert.Equal(150_000, before.Amount);

            await ChangeOkAsync(admin, s => s with { CardNumber = OtherCard, CardHolderName = "مریم کریمی", BankName = "ملت" });

            var old = (await GetOrderAsync(shopper, first.AccessToken)).PaymentInstructions!;
            Assert.Equal(before, old); // the existing order still shows the card it was placed with

            var other = NewShopper();
            await other.AddAsync(variant, 1);
            var second = await PlaceOkAsync(other, method: "cardToCard");
            var fresh = (await GetOrderAsync(other, second.AccessToken)).PaymentInstructions!;
            Assert.Equal(OtherCard, fresh.CardNumber);
            Assert.Equal("مریم کریمی", fresh.HolderName);
            Assert.Equal("ملت", fresh.BankName);
        }
        finally
        {
            await RestoreAsync(admin);
        }
    }

    [Fact]
    public async Task CashOnDeliveryOrders_CarryNoCardSnapshot()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 100_000, 5);
        var shopper = NewShopper();
        await shopper.AddAsync(variant, 1);
        var placed = await PlaceOkAsync(shopper);

        var stored = await Fixture.WithDbAsync(db => db.Orders.Where(o => o.Number == placed.Number)
            .Select(o => new { o.PaymentCardNumber, o.PaymentCardHolder, o.PaymentBankName }).SingleAsync());

        Assert.Null(stored.PaymentCardNumber);
        Assert.Null(stored.PaymentCardHolder);
        Assert.Null(stored.PaymentBankName);
    }

    [Fact]
    public async Task CardToCard_IsHidden_AndRefused_WhileTheCardIsNotValidlyConfigured()
    {
        var admin = await LoginAdminAsync();
        var variant = await CreateVariantAsync(admin, 100_000, 10);
        var provider = Fixture.Factory.Services.GetRequiredService<IStoreSettings>();
        try
        {
            // A fresh install: switched on, but no card yet (the API can't save this state; only seeding can produce it).
            await Fixture.WithDbAsync(async db => await db.StoreSettings.ExecuteUpdateAsync(s => s
                .SetProperty(x => x.CardNumber, "").SetProperty(x => x.CardHolderName, "").SetProperty(x => x.CardToCardEnabled, true)));
            provider.Invalidate();

            var shopper = NewShopper();
            await shopper.AddAsync(variant, 1);
            var cart = await shopper.CartAsync();
            var raw = await (await shopper.SendAsync(HttpMethod.Get, "/api/v1/store/cart")).Content.ReadAsStringAsync();
            Assert.Contains("\"cardToCard\":false", raw);
            Assert.Contains("\"cashOnDelivery\":true", raw);
            Assert.False((await StoreInfoAsync(NewClient())).CardToCard);

            var (refused, _) = await PlaceAsync(shopper, method: "cardToCard", expectedTotal: cart.Total);
            var problem = await AssertProblemAsync(refused, 400, "validation_failed");
            Assert.True(problem.Errors!.ContainsKey("paymentMethod"));
            Assert.Equal(10, await StockOfAsync(variant)); // nothing was taken

            var (cod, _) = await PlaceAsync(shopper, method: "cashOnDelivery");
            Assert.Equal(HttpStatusCode.Created, cod.StatusCode); // cash on delivery still works
        }
        finally
        {
            await RestoreAsync(admin);
        }

        Assert.True((await StoreInfoAsync(NewClient())).CardToCard); // and card-to-card is back once configured
    }
}
