using System.Net;

namespace Shop.IntegrationTests;

public sealed class AuthorizationTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Customer_CanReadOwnProfile()
    {
        var (client, email, _) = await RegisterCustomerAsync();

        var response = await client.GetAsync("/api/v1/customer/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await ReadAsync<ProfileBody>(response);
        Assert.Equal(email, profile.Email);
        Assert.Equal("Test Customer", profile.FullName);
        Assert.Equal("09120000000", profile.Phone);
    }

    [Fact]
    public async Task EachCustomer_OnlySeesTheirOwnProfile()
    {
        var (first, firstEmail, _) = await RegisterCustomerAsync();
        var (second, secondEmail, _) = await RegisterCustomerAsync();

        var firstProfile = await ReadAsync<ProfileBody>(await first.GetAsync("/api/v1/customer/profile"));
        var secondProfile = await ReadAsync<ProfileBody>(await second.GetAsync("/api/v1/customer/profile"));

        Assert.Equal(firstEmail, firstProfile.Email);
        Assert.Equal(secondEmail, secondProfile.Email);
        Assert.NotEqual(firstProfile.CustomerId, secondProfile.CustomerId);
    }

    [Fact]
    public async Task Customer_CannotAccessAdminEndpoint()
    {
        var (client, _, _) = await RegisterCustomerAsync();

        var response = await client.GetAsync("/api/v1/admin/ping");

        await AssertProblemAsync(response, 403);
    }

    [Fact]
    public async Task Admin_CanAccessAdminEndpoint()
    {
        var admin = await LoginAdminAsync();

        var response = await admin.GetAsync("/api/v1/admin/ping");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Admin_HasAdminRoleOnly_AndIsNotACustomer()
    {
        var admin = await LoginAdminAsync();

        var account = await ReadAsync<AccountBody>(await admin.GetAsync("/api/v1/auth/me"));
        var customerArea = await admin.GetAsync("/api/v1/customer/profile");

        Assert.Equal(["Admin"], account.Roles);
        await AssertProblemAsync(customerArea, 403);
    }

    [Fact]
    public async Task RolePermissions_AreSyncedFromCode_AndStartupSyncIsIdempotent()
    {
        async Task<string[]> AdminPermissions() => await Fixture.WithDbAsync(db => Task.FromResult(
            (from rc in db.RoleClaims
             join r in db.Roles on rc.RoleId equals r.Id
             where r.Name == "Admin" && rc.ClaimType == "permission"
             select rc.ClaimValue!).OrderBy(v => v).ToArray()));

        var before = await AdminPermissions();
        using (var scope = Fixture.Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<Shop.Infrastructure.Persistence.DatabaseInitializer>()
                .InitializeAsync(CancellationToken.None);
        }

        var after = await AdminPermissions();

        Assert.Equal(Shop.Domain.Security.Permissions.All.OrderBy(p => p), before);
        Assert.Equal(before, after);
        var customerClaims = await Fixture.WithDbAsync(db => Task.FromResult(
            (from rc in db.RoleClaims join r in db.Roles on rc.RoleId equals r.Id where r.Name == "Customer" select rc).Count()));
        Assert.Equal(0, customerClaims);
    }
}
