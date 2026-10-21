using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Shop.Domain.Customers;
using Shop.Infrastructure.Identity;

namespace Shop.IntegrationTests;

public sealed class PersistenceTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    [Fact]
    public async Task Registration_PersistsUserRoleAndCustomerProfile_WithoutStoringThePassword()
    {
        var email = TestData.NewEmail();
        var password = TestData.NewPassword();
        var response = await PostAsync(NewClient(), "/api/v1/auth/register", new { email, password, fullName = "Persisted Person", phone = "09123333333" });
        Assert.Equal(System.Net.HttpStatusCode.Created, response.StatusCode);

        var saved = await Fixture.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Email == email);
            var customer = await db.Customers.SingleAsync(c => c.UserId == user.Id);
            var roles = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id where ur.UserId == user.Id select r.Name).ToListAsync();
            return (user, customer, roles);
        });

        Assert.Equal("Persisted Person", saved.customer.FullName);
        Assert.Equal("09123333333", saved.customer.Phone);
        Assert.Equal(["Customer"], saved.roles);
        Assert.NotNull(saved.user.PasswordHash);
        Assert.DoesNotContain(password, saved.user.PasswordHash, StringComparison.Ordinal);
        Assert.NotEqual(default, saved.customer.CreatedAtUtc);
        Assert.Equal(DateTimeKind.Utc, DateTime.SpecifyKind(saved.customer.CreatedAtUtc, DateTimeKind.Utc).Kind);
        Assert.True(saved.customer.CreatedAtUtc > DateTime.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task Database_RejectsTwoUsersWithTheSameNormalizedEmail()
    {
        var email = TestData.NewEmail();

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => Fixture.WithDbAsync(async db =>
        {
            // Different user names, so only the email constraint can reject the second row.
            db.Users.Add(NewUser("first-" + Guid.NewGuid().ToString("N"), email));
            db.Users.Add(NewUser("second-" + Guid.NewGuid().ToString("N"), email));
            await db.SaveChangesAsync();
            return 0;
        }));

        var sql = Assert.IsType<SqlException>(ex.InnerException);
        Assert.Contains(sql.Number, new[] { 2601, 2627 });
        Assert.Contains("UX_Users_NormalizedEmail", sql.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Database_RejectsASecondCustomerProfileForTheSameUser()
    {
        var (_, email, _) = await RegisterCustomerAsync();

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => Fixture.WithDbAsync(async db =>
        {
            var userId = (await db.Users.SingleAsync(u => u.Email == email)).Id;
            db.Customers.Add(Customer.Create(userId, "Duplicate Profile", null, DateTime.UtcNow));
            await db.SaveChangesAsync();
            return 0;
        }));

        Assert.IsType<SqlException>(ex.InnerException);
    }

    [Fact]
    public async Task Database_RejectsACustomerProfileWithoutAnExistingUser()
    {
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => Fixture.WithDbAsync(async db =>
        {
            db.Customers.Add(Customer.Create(Guid.NewGuid(), "Orphan", null, DateTime.UtcNow));
            await db.SaveChangesAsync();
            return 0;
        }));

        Assert.Contains("FOREIGN KEY", ex.InnerException!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Customer_UpdatesAreProtectedByRowVersion()
    {
        var (_, email, _) = await RegisterCustomerAsync();
        Guid customerId = await Fixture.WithDbAsync(async db =>
            (await db.Customers.AsNoTracking().Join(db.Users, c => c.UserId, u => u.Id, (c, u) => new { c.Id, u.Email }).SingleAsync(x => x.Email == email)).Id);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(async () =>
        {
            using var firstScope = Fixture.Factory.Services.CreateScope();
            using var secondScope = Fixture.Factory.Services.CreateScope();
            var first = firstScope.ServiceProvider.GetRequiredService<Shop.Infrastructure.Persistence.ShopDbContext>();
            var second = secondScope.ServiceProvider.GetRequiredService<Shop.Infrastructure.Persistence.ShopDbContext>();

            var a = await first.Customers.SingleAsync(c => c.Id == customerId);
            var b = await second.Customers.SingleAsync(c => c.Id == customerId);
            a.UpdateProfile("Edited First", null);
            b.UpdateProfile("Edited Second", null);
            await first.SaveChangesAsync();
            await second.SaveChangesAsync();
        });
    }

    private static ApplicationUser NewUser(string userName, string email) => new()
    {
        Id = Guid.NewGuid(),
        UserName = userName,
        NormalizedUserName = userName.ToUpperInvariant(),
        Email = email,
        NormalizedEmail = email.ToUpperInvariant(),
        CreatedAtUtc = DateTime.UtcNow,
        SecurityStamp = Guid.NewGuid().ToString(),
    };
}
