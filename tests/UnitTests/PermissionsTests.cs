using Shop.Domain.Security;

namespace Shop.UnitTests;

public class PermissionsTests
{
    [Fact]
    public void AllPermissions_AreUnique_AndFollowResourceDotActionFormat()
    {
        Assert.Equal(Permissions.All.Count, Permissions.All.Distinct(StringComparer.Ordinal).Count());
        Assert.All(Permissions.All, p =>
        {
            var parts = p.Split('.');
            Assert.Equal(2, parts.Length);
            Assert.All(parts, part => Assert.False(string.IsNullOrWhiteSpace(part)));
        });
    }

    [Fact]
    public void Admin_HasEveryPermission()
    {
        Assert.Equal(
            Permissions.All.OrderBy(p => p),
            Roles.PermissionsFor(Roles.Admin).OrderBy(p => p));
    }

    [Fact]
    public void Customer_HasNoAdminPermissions()
    {
        Assert.Empty(Roles.PermissionsFor(Roles.Customer));
    }

    [Fact]
    public void UnknownRole_HasNoPermissions()
    {
        Assert.Empty(Roles.PermissionsFor("Nobody"));
    }
}
