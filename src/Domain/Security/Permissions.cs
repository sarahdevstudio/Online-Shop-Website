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
    public const string CatalogManage = "Catalog.Manage";
    public const string OrdersManage = "Orders.Manage";
    public const string ContentManage = "Content.Manage";
    public const string BlogManage = "Blog.Manage";
    public const string SiteManage = "Site.Manage";
    public const string TranslationsManage = "Translations.Manage";

    /// <summary>Releasing or deleting an old path (redirect). Changing an entity's own path needs that entity's permission (docs/11 C-4).</summary>
    public const string RoutesManage = "Routes.Manage";

    public static IReadOnlyList<string> All { get; } = [UserManage, SettingsManage, CatalogManage, OrdersManage, ContentManage, BlogManage, SiteManage, TranslationsManage, RoutesManage];
}
