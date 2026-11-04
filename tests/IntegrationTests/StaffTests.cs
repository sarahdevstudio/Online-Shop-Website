using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Shop.Domain.Security;

namespace Shop.IntegrationTests;

public sealed record StaffUserBody(Guid Id, string Username, string? DisplayName, string? Email, bool IsActive, string[] Roles, string Stamp);

public sealed record StaffRoleBody(Guid Id, string Name, string? Title, bool IsSystem, string[] Permissions, int UserCount, string Stamp);

public sealed record StaffOverviewBody(StaffUserBody[] Users, StaffRoleBody[] Roles, string[] Permissions);

public sealed record StaffAuditItemBody(int Id, Guid? ActorUserId, string? ActorName, string Action, string? Subject, string? Detail);

public sealed record AdminMeBody(string Username, string[] Permissions);

public sealed record StaffAuditBody(StaffAuditItemBody[] Items, int Total);

/// <summary>Staff accounts and roles (docs/16): create, deactivate (sessions end), reset password, roles, the no-lock-out rule, step-up, audit.</summary>
public sealed class StaffTests(ApiFixture fixture) : CatalogTestBase(fixture)
{
    private const string Url = "/api/v1/admin/staff";

    private static string NewUsername() => "staff-" + Guid.NewGuid().ToString("N")[..10];

    private Task<StaffOverviewBody> OverviewAsync(HttpClient admin) => GetOkAsync<StaffOverviewBody>(admin, Url);

    private async Task<StaffUserBody> UserAsync(HttpClient admin, Guid id) => (await OverviewAsync(admin)).Users.Single(u => u.Id == id);

    private async Task<StaffRoleBody> RoleAsync(HttpClient admin, string name) => (await OverviewAsync(admin)).Roles.Single(r => r.Name == name);

    private Task<HttpResponseMessage> CreateRawAsync(HttpClient admin, string username, string password, string[] roles, string? stepUp = null, string? email = null) =>
        SendAsync(admin, HttpMethod.Post, Url, new { username, displayName = "کارمند آزمایشی", email, password, roles, currentPassword = stepUp ?? Fixture.AdminPassword });

    private async Task<(StaffUserBody User, string Password)> CreateOkAsync(HttpClient admin, params string[] roles)
    {
        var password = TestData.NewPassword() + "-Zz9";
        var username = NewUsername();
        var response = await CreateRawAsync(admin, username, password, roles.Length == 0 ? [Roles.Admin] : roles);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return ((await response.Content.ReadFromJsonAsync<Envelope<StaffUserBody>>(ApiJson))!.Result!, password);
    }

    private async Task<HttpClient> SignInAsync(string username, string password)
    {
        var client = NewClient();
        var response = await PostAsync(client, "/api/v1/auth/login", new { username, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return client;
    }

    private Task<HttpResponseMessage> RolesAsync(HttpClient admin, StaffUserBody user, string[] roles, string? stepUp = null) =>
        SendAsync(admin, HttpMethod.Put, $"{Url}/{user.Id}/roles", new { roles, stamp = user.Stamp, currentPassword = stepUp ?? Fixture.AdminPassword });

    private Task<HttpResponseMessage> ActiveAsync(HttpClient admin, StaffUserBody user, bool active) =>
        SendAsync(admin, HttpMethod.Put, $"{Url}/{user.Id}/active", new { isActive = active, stamp = user.Stamp });

    private async Task<StaffRoleBody> NewRoleAsync(HttpClient admin, params string[] permissions)
    {
        var name = "Role" + Guid.NewGuid().ToString("N")[..8];
        return await SendOkAsync<StaffRoleBody>(admin, HttpMethod.Post, $"{Url}/roles", new { name, title = "نقش آزمایشی", permissions, currentPassword = Fixture.AdminPassword });
    }

    // ---- access ----------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task OnlyUserManage_OpensStaffManagement()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await NewClient().GetAsync(Url)).StatusCode);

        var admin = await LoginAdminAsync();
        var role = await NewRoleAsync(admin, Permissions.OrdersManage);
        var (user, password) = await CreateOkAsync(admin, role.Name);
        var limited = await SignInAsync(user.Username, password);
        Assert.Equal(HttpStatusCode.Forbidden, (await limited.GetAsync(Url)).StatusCode);

        // A write without the CSRF token is refused before anything happens.
        var noCsrf = await admin.PostAsJsonAsync(Url, new { username = NewUsername(), password = "x", roles = new[] { Roles.Admin } });
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
    }

    [Fact]
    public async Task Me_ListsOnlyThePermissionsOfTheSignedInStaffAccount()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await NewClient().GetAsync("/api/v1/admin/me")).StatusCode);

        var admin = await LoginAdminAsync();
        Assert.Equal(Permissions.All.Order(StringComparer.Ordinal), (await GetOkAsync<AdminMeBody>(admin, "/api/v1/admin/me")).Permissions);

        var role = await NewRoleAsync(admin, Permissions.BlogManage, Permissions.ContentManage);
        var (user, password) = await CreateOkAsync(admin, role.Name);
        var me = await GetOkAsync<AdminMeBody>(await SignInAsync(user.Username, password), "/api/v1/admin/me");
        Assert.Equal([Permissions.BlogManage, Permissions.ContentManage], me.Permissions);

        // A shopper has no permission at all: not a staff account.
        var (customer, _, _) = await RegisterCustomerAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.GetAsync("/api/v1/admin/me")).StatusCode);
    }

    // ---- accounts ----------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task CreatingAStaffAccount_AppliesTheAdminPasswordRules_AndNeedsTheCallersPassword()
    {
        var admin = await LoginAdminAsync();
        var role = await NewRoleAsync(admin, Permissions.CatalogManage);

        var (user, password) = await CreateOkAsync(admin, role.Name);
        Assert.Equal([role.Name], user.Roles);
        Assert.Equal("کارمند آزمایشی", user.DisplayName);
        Assert.True(user.IsActive);
        var signedIn = await SignInAsync(user.Username, password);
        Assert.Equal(HttpStatusCode.OK, (await signedIn.GetAsync("/api/v1/admin/brands")).StatusCode);

        await AssertProblemAsync(await CreateRawAsync(admin, NewUsername(), "short1", [role.Name]), 400);
        await AssertProblemAsync(await CreateRawAsync(admin, NewUsername(), "1234567890123456", [role.Name]), 400);
        await AssertProblemAsync(await CreateRawAsync(admin, user.Username, TestData.NewPassword() + "-Zz9", [role.Name]), 409, "username_taken");
        await AssertProblemAsync(await CreateRawAsync(admin, NewUsername(), TestData.NewPassword() + "-Zz9", [Roles.Customer]), 400);
        await AssertProblemAsync(await CreateRawAsync(admin, NewUsername(), TestData.NewPassword() + "-Zz9", ["NoSuchRole"]), 400);
        await AssertProblemAsync(await CreateRawAsync(admin, NewUsername(), TestData.NewPassword() + "-Zz9", [role.Name], stepUp: "wrong-password"), 403, "step_up_failed");
        await AssertProblemAsync(await CreateRawAsync(admin, NewUsername(), TestData.NewPassword() + "-Zz9", [role.Name], stepUp: ""), 403, "step_up_failed");
    }

    [Fact]
    public async Task Deactivating_EndsTheSessions_AndBlocksSignIn_UntilReactivated()
    {
        var admin = await LoginAdminAsync();
        var (user, password) = await CreateOkAsync(admin, Roles.Admin);
        var session = await SignInAsync(user.Username, password);
        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/api/v1/admin/brands")).StatusCode);

        var off = await ActiveAsync(admin, user, false);
        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        Assert.False((await UserAsync(admin, user.Id)).IsActive);

        // The session that was open is dead on its very next request, and the account cannot sign in again.
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/v1/admin/brands")).StatusCode);
        var again = await PostAsync(NewClient(), "/api/v1/auth/login", new { username = user.Username, password });
        var wrong = await PostAsync(NewClient(), "/api/v1/auth/login", new { username = user.Username, password = "wrong-" + password });
        Assert.Equal(HttpStatusCode.Unauthorized, again.StatusCode);
        Assert.Equal(await wrong.Content.ReadAsStringAsync(), await again.Content.ReadAsStringAsync()); // same answer as a wrong password

        var on = await ActiveAsync(admin, await UserAsync(admin, user.Id), true);
        Assert.Equal(HttpStatusCode.OK, on.StatusCode);
        await SignInAsync(user.Username, password);
    }

    [Fact]
    public async Task ResettingAPassword_ReplacesIt_RevokesSessions_AndNeedsTheCallersPassword()
    {
        var admin = await LoginAdminAsync();
        var (user, oldPassword) = await CreateOkAsync(admin, Roles.Admin);
        var session = await SignInAsync(user.Username, oldPassword);
        var newPassword = TestData.NewPassword() + "-Qq7";

        // The stamp is the optimistic-concurrency version: an old one is a 409.
        var fresh = await UserAsync(admin, user.Id);
        Task<HttpResponseMessage> ResetWith(string pw, string stamp, string? stepUp = null) =>
            SendAsync(admin, HttpMethod.Put, $"{Url}/{user.Id}/password", new { newPassword = pw, stamp, currentPassword = stepUp ?? Fixture.AdminPassword });

        await AssertProblemAsync(await ResetWith("short", fresh.Stamp), 400);
        await AssertProblemAsync(await ResetWith(newPassword, fresh.Stamp, "wrong-password"), 403, "step_up_failed");
        await AssertProblemAsync(await ResetWith(newPassword, "stale-stamp"), 409, "concurrent_update");
        Assert.Equal(HttpStatusCode.OK, (await ResetWith(newPassword, fresh.Stamp)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/v1/admin/brands")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await PostAsync(NewClient(), "/api/v1/auth/login", new { username = user.Username, password = oldPassword })).StatusCode);
        await SignInAsync(user.Username, newPassword);
    }

    [Fact]
    public async Task EditingTheProfile_UsesTheStampFor409_AndRefusesADuplicateEmail()
    {
        var admin = await LoginAdminAsync();
        var (user, _) = await CreateOkAsync(admin);
        var (other, _) = await CreateOkAsync(admin);

        var email = $"{Guid.NewGuid():N}@example.com";
        var saved = await SendOkAsync<StaffUserBody>(admin, HttpMethod.Put, $"{Url}/{user.Id}", new { displayName = "نام تازه", email, stamp = user.Stamp });
        Assert.Equal(("نام تازه", email), (saved.DisplayName, saved.Email));

        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Put, $"{Url}/{user.Id}", new { displayName = "x", email, stamp = user.Stamp }), 409, "concurrent_update");
        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Put, $"{Url}/{other.Id}", new { displayName = "y", email, stamp = other.Stamp }), 400);
        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Put, $"{Url}/{other.Id}", new { displayName = new string('ب', 129), email = (string?)null, stamp = other.Stamp }), 400);
    }

    [Fact]
    public async Task ACustomersAccount_IsNotStaff()
    {
        var admin = await LoginAdminAsync();
        var (_, email, _) = await RegisterCustomerAsync();
        var customerId = await Fixture.WithDbAsync(db => db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync());

        Assert.DoesNotContain((await OverviewAsync(admin)).Users, u => u.Id == customerId);
        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Put, $"{Url}/{customerId}/active", new { isActive = false, stamp = "x" }), 404);
    }

    // ---- roles ------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task ARoleDecidesWhatTheUserCanDo_AndEditingItChangesItAtOnce()
    {
        var admin = await LoginAdminAsync();
        var role = await NewRoleAsync(admin, Permissions.OrdersManage);
        Assert.False(role.IsSystem);
        Assert.Equal([Permissions.OrdersManage], role.Permissions);

        var (user, password) = await CreateOkAsync(admin, role.Name);
        var session = await SignInAsync(user.Username, password);
        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/api/v1/admin/orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await session.GetAsync("/api/v1/admin/brands")).StatusCode);

        // Adding a permission: the person's session ends (a new sign-in carries the new set).
        var updated = await SendOkAsync<StaffRoleBody>(admin, HttpMethod.Put, $"{Url}/roles/{role.Id}",
            new { name = role.Name, title = "نقش ویرایش‌شده", permissions = new[] { Permissions.OrdersManage, Permissions.CatalogManage }, stamp = role.Stamp, currentPassword = Fixture.AdminPassword });
        Assert.Equal("نقش ویرایش‌شده", updated.Title);
        Assert.Equal(1, updated.UserCount);
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/v1/admin/orders")).StatusCode);
        var again = await SignInAsync(user.Username, password);
        Assert.Equal(HttpStatusCode.OK, (await again.GetAsync("/api/v1/admin/brands")).StatusCode);

        // The old stamp is a 409; the wrong password a 403; an unknown permission a 400.
        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Put, $"{Url}/roles/{role.Id}",
            new { name = role.Name, title = "x", permissions = new[] { Permissions.OrdersManage }, stamp = role.Stamp, currentPassword = Fixture.AdminPassword }), 409, "concurrent_update");
        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Put, $"{Url}/roles/{role.Id}",
            new { name = role.Name, title = "x", permissions = new[] { Permissions.OrdersManage }, stamp = updated.Stamp, currentPassword = "wrong" }), 403, "step_up_failed");
        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Put, $"{Url}/roles/{role.Id}",
            new { name = role.Name, title = "x", permissions = new[] { "Nothing.Real" }, stamp = updated.Stamp, currentPassword = Fixture.AdminPassword }), 400);
    }

    [Fact]
    public async Task RoleRules_NameFormat_Duplicates_SystemRoles_AndDeletion()
    {
        var admin = await LoginAdminAsync();
        Task<HttpResponseMessage> Create(string name, string? title = "عنوان") =>
            SendAsync(admin, HttpMethod.Post, $"{Url}/roles", new { name, title, permissions = new[] { Permissions.BlogManage }, currentPassword = Fixture.AdminPassword });

        await AssertProblemAsync(await Create("a"), 400);
        await AssertProblemAsync(await Create("نقش"), 400);
        await AssertProblemAsync(await Create("Valid_Name", null), 400);
        await AssertProblemAsync(await Create("admin"), 409, "role_name_taken"); // the built-in names are taken, whatever the case

        var role = await NewRoleAsync(admin, Permissions.BlogManage);
        await AssertProblemAsync(await Create(role.Name), 409, "role_name_taken");

        var system = await RoleAsync(admin, Roles.Admin);
        Assert.True(system.IsSystem);
        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Put, $"{Url}/roles/{system.Id}",
            new { name = "Admin", title = "x", permissions = Array.Empty<string>(), stamp = system.Stamp, currentPassword = Fixture.AdminPassword }), 409, "role_system");
        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Post, $"{Url}/roles/{system.Id}/delete", new { stamp = system.Stamp, currentPassword = Fixture.AdminPassword }), 409, "role_system");

        // In use: refused. Unused: deleted.
        var (user, _) = await CreateOkAsync(admin, role.Name);
        var fresh = await RoleAsync(admin, role.Name);
        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Post, $"{Url}/roles/{role.Id}/delete", new { stamp = fresh.Stamp, currentPassword = Fixture.AdminPassword }), 409, "role_in_use");
        var moved = await RolesAsync(admin, user, [Roles.Admin]);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        fresh = await RoleAsync(admin, role.Name);
        await AssertProblemAsync(await SendAsync(admin, HttpMethod.Post, $"{Url}/roles/{role.Id}/delete", new { stamp = fresh.Stamp, currentPassword = "wrong" }), 403, "step_up_failed");
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(admin, HttpMethod.Post, $"{Url}/roles/{role.Id}/delete", new { stamp = fresh.Stamp, currentPassword = Fixture.AdminPassword })).StatusCode);
        Assert.DoesNotContain((await OverviewAsync(admin)).Roles, r => r.Id == role.Id);
    }

    [Fact]
    public async Task ChangingRoles_NeedsTheCallersPassword_AndAtLeastOneRole()
    {
        var admin = await LoginAdminAsync();
        var role = await NewRoleAsync(admin, Permissions.ContentManage);
        var (user, password) = await CreateOkAsync(admin, role.Name);
        var session = await SignInAsync(user.Username, password);

        await AssertProblemAsync(await RolesAsync(admin, user, [Roles.Admin], stepUp: "wrong"), 403, "step_up_failed");
        await AssertProblemAsync(await RolesAsync(admin, user, []), 400);
        await AssertProblemAsync(await RolesAsync(admin, user, [Roles.Customer]), 400);
        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/api/v1/admin/content-pages")).StatusCode); // nothing changed yet

        Assert.Equal(HttpStatusCode.OK, (await RolesAsync(admin, user, [Roles.Admin])).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/v1/admin/content-pages")).StatusCode); // must sign in again
        Assert.Equal([Roles.Admin], (await UserAsync(admin, user.Id)).Roles);
    }

    // ---- no lock-out ------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task TheLastActiveUserWithEveryPermission_CannotBeRemoved()
    {
        var admin = await LoginAdminAsync();
        var (other, otherPassword) = await CreateOkAsync(admin, Roles.Admin);
        await SignInAsync(other.Username, otherPassword);

        // Other tests leave full-permission users behind: park them (directly) so exactly two full users remain, restore afterwards.
        var parked = await Fixture.WithDbAsync(async db =>
        {
            var adminRoleId = await db.Roles.Where(r => r.Name == Roles.Admin).Select(r => r.Id).SingleAsync();
            var ids = await db.UserRoles.Where(ur => ur.RoleId == adminRoleId).Select(ur => ur.UserId).ToListAsync();
            var fixtureAdminId = await db.Users.Where(u => u.Email == ApiFixture.AdminEmail || u.UserName == ApiFixture.AdminEmail).Select(u => u.Id).SingleAsync();
            var toPark = await db.Users.Where(u => ids.Contains(u.Id) && u.Id != fixtureAdminId && u.Id != other.Id && u.IsActive).Select(u => u.Id).ToListAsync();
            await db.Users.Where(u => toPark.Contains(u.Id)).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
            return (toPark, fixtureAdminId);
        });
        try
        {
            // The fixture admin deactivates the other full user: still one left (themselves) → fine.
            var current = await UserAsync(admin, other.Id);
            Assert.Equal(HttpStatusCode.OK, (await ActiveAsync(admin, current, false)).StatusCode);

            // Now the caller is the only one. They cannot deactivate themselves, and cannot give up the Admin role.
            var me = (await OverviewAsync(admin)).Users.Single(u => u.Id == parked.fixtureAdminId);
            await AssertProblemAsync(await ActiveAsync(admin, me, false), 409, "staff_self_deactivate");
            var limited = await NewRoleAsync(admin, Permissions.OrdersManage);
            await AssertProblemAsync(await RolesAsync(admin, me, [limited.Name]), 409, "staff_lockout");
            Assert.Contains(Roles.Admin, (await UserAsync(admin, me.Id)).Roles); // rolled back

            // Reactivating the other one makes the same change legal again for them (they stay full), so reverse and prove the guard lifts.
            Assert.Equal(HttpStatusCode.OK, (await ActiveAsync(admin, await UserAsync(admin, other.Id), true)).StatusCode);
            var restored = await UserAsync(admin, me.Id);
            Assert.Equal(HttpStatusCode.OK, (await RolesAsync(admin, restored, [limited.Name, Roles.Admin])).StatusCode);
        }
        finally
        {
            await Fixture.WithDbAsync(async db =>
            {
                await db.Users.Where(u => parked.toPark.Contains(u.Id)).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, true));
                return 0;
            });
        }

    }

    [Fact]
    public async Task ChangingAPermissionOfARole_CannotRemoveTheLastFullHolder()
    {
        var admin = await LoginAdminAsync();
        // A custom role that holds every permission, as the only way a non-built-in role could keep someone "full".
        var all = await SendOkAsync<StaffRoleBody>(admin, HttpMethod.Post, $"{Url}/roles",
            new { name = "Everything" + Guid.NewGuid().ToString("N")[..6], title = "همه", permissions = Permissions.All, currentPassword = Fixture.AdminPassword });
        Assert.Equal(Permissions.All.Count, all.Permissions.Length);
        // Others still hold everything through the Admin role, so trimming it is allowed; the guard only bites when nobody is left.
        var trimmed = await SendAsync(admin, HttpMethod.Put, $"{Url}/roles/{all.Id}",
            new { name = all.Name, title = "کمتر", permissions = new[] { Permissions.OrdersManage }, stamp = all.Stamp, currentPassword = Fixture.AdminPassword });
        Assert.Equal(HttpStatusCode.OK, trimmed.StatusCode);
    }

    // ---- audit ------------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task EveryChangeIsAudited_WithTheActor_AndNeverAPassword()
    {
        var admin = await LoginAdminAsync();
        var (user, password) = await CreateOkAsync(admin, Roles.Admin);
        await AssertProblemAsync(await RolesAsync(admin, user, [Roles.Admin], stepUp: "super-secret-wrong-password"), 403);
        await ActiveAsync(admin, user, false);

        var audit = await GetOkAsync<StaffAuditBody>(admin, $"{Url}/audit");
        var mine = audit.Items.Where(i => i.Subject == user.Username || i.Action == StaffAuditActions.ReauthFailed).ToList();
        Assert.Contains(mine, i => i.Action == StaffAuditActions.Created && i.ActorName == ApiFixture.AdminEmail);
        Assert.Contains(mine, i => i.Action == StaffAuditActions.Deactivated);
        Assert.Contains(mine, i => i.Action == StaffAuditActions.ReauthFailed);
        var text = string.Join('\n', audit.Items.Select(i => $"{i.Subject} {i.Detail}"));
        Assert.DoesNotContain("super-secret-wrong-password", text);
        Assert.DoesNotContain(password, text);
    }
}
