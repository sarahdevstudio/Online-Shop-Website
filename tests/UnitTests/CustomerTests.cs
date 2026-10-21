using Shop.Domain.Customers;

namespace Shop.UnitTests;

public class CustomerTests
{
    private static readonly DateTime Now = new(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_TrimsNameAndPhone_AndStoresUtcTimestamp()
    {
        var userId = Guid.NewGuid();

        var customer = Customer.Create(userId, "  Sara Ahmadi ", " 09120000000 ", Now);

        Assert.NotEqual(Guid.Empty, customer.Id);
        Assert.Equal(userId, customer.UserId);
        Assert.Equal("Sara Ahmadi", customer.FullName);
        Assert.Equal("09120000000", customer.Phone);
        Assert.Equal(Now, customer.CreatedAtUtc);
    }

    [Fact]
    public void Create_BlankPhone_IsStoredAsNull()
    {
        var customer = Customer.Create(Guid.NewGuid(), "Sara", "   ", Now);

        Assert.Null(customer.Phone);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutName_Throws(string name)
    {
        Assert.Throws<ArgumentException>(() => Customer.Create(Guid.NewGuid(), name, null, Now));
    }

    [Fact]
    public void Create_WithEmptyUserId_Throws()
    {
        Assert.Throws<ArgumentException>(() => Customer.Create(Guid.Empty, "Sara", null, Now));
    }

    [Fact]
    public void Create_NameLongerThanLimit_Throws()
    {
        var tooLong = new string('x', Customer.FullNameMaxLength + 1);

        Assert.Throws<ArgumentException>(() => Customer.Create(Guid.NewGuid(), tooLong, null, Now));
    }

    [Fact]
    public void UpdateProfile_ChangesNameAndPhone()
    {
        var customer = Customer.Create(Guid.NewGuid(), "Sara", null, Now);

        customer.UpdateProfile("Sara Ahmadi", "09121111111");

        Assert.Equal("Sara Ahmadi", customer.FullName);
        Assert.Equal("09121111111", customer.Phone);
    }
}
