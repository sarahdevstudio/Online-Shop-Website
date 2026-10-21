namespace Shop.Domain.Security;

/// <summary>
/// Single registry of admin permissions ("Resource.Action"). Later phases add their own
/// (Product.Update, Order.Update, ...) here; only permissions that guard something are listed.
/// </summary>
public static class Permissions
{
    public const string ClaimType = "permission";

    public const string UserManage = "User.Manage";
    public const string SettingsManage = "Settings.Manage";

    public static IReadOnlyList<string> All { get; } = [UserManage, SettingsManage];
}
