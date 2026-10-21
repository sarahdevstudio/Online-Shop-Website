namespace Shop.Domain.Security;

public static class Roles
{
    public const string Customer = "Customer";
    public const string Admin = "Admin";

    public static IReadOnlyList<string> All { get; } = [Customer, Admin];

    /// <summary>
    /// Permissions granted to each role. Synchronised into the database on startup, so adding a
    /// permission is a code change plus a restart, not a schema migration.
    /// </summary>
    public static IReadOnlyCollection<string> PermissionsFor(string role) => role switch
    {
        Admin => Permissions.All,
        _ => [],
    };
}
