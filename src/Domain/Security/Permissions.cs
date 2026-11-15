namespace Shop.Domain.Security;

/// <summary>
/// Single registry of admin permissions ("Resource.Action"). Granular per action (View/Create/Update/Delete) where
/// the resource supports them, plus named special actions for things that don't fit that shape (a payment
/// confirmation, a release, a moderation decision, ...). <see cref="PermissionsReplacedByManage"/> maps each
/// retired coarse "X.Manage" permission to the granular ones it is replaced by, for the one-time role-claim
/// migration (<c>PermissionMigration</c>) — nobody loses access when this list changes.
/// </summary>
public static class Permissions
{
    public const string ClaimType = "permission";

    public const string CatalogView = "Catalog.View";
    public const string CatalogCreate = "Catalog.Create";
    public const string CatalogUpdate = "Catalog.Update";
    public const string CatalogDelete = "Catalog.Delete";
    public const string CatalogImport = "Catalog.Import";
    public const string CatalogExport = "Catalog.Export";

    public const string ContentView = "Content.View";
    public const string ContentUpdate = "Content.Update";

    public const string BlogView = "Blog.View";
    public const string BlogCreate = "Blog.Create";
    public const string BlogUpdate = "Blog.Update";
    public const string BlogDelete = "Blog.Delete";

    public const string OrdersView = "Orders.View";
    public const string OrdersUpdate = "Orders.Update";
    public const string OrdersConfirmPayment = "Orders.ConfirmPayment";
    public const string OrdersCancel = "Orders.Cancel";
    public const string OrdersPrint = "Orders.Print";

    public const string SettingsView = "Settings.View";
    public const string SettingsUpdate = "Settings.Update";

    /// <summary>The card-to-card payment fields specifically (CardNumber, CardHolderName, BankName, CardToCardEnabled).</summary>
    public const string SettingsPayment = "Settings.Payment";

    public const string SiteView = "Site.View";
    public const string SiteUpdate = "Site.Update";

    public const string RoutesView = "Routes.View";
    public const string RoutesUpdate = "Routes.Update";

    /// <summary>Releasing or deleting an old path (redirect). Changing an entity's own path needs that entity's own permission (docs/11 C-4).</summary>
    public const string RoutesRelease = "Routes.Release";

    public const string ReviewsView = "Reviews.View";

    /// <summary>Approving or rejecting the reviews guests submit.</summary>
    public const string ReviewsModerate = "Reviews.Moderate";

    public const string TranslationsView = "Translations.View";
    public const string TranslationsUpdate = "Translations.Update";

    /// <summary>Staff accounts and roles, kept as one permission (not split): the person who can create an admin can do all of it.</summary>
    public const string UserManage = "User.Manage";

    public static IReadOnlyList<string> All { get; } =
    [
        CatalogView, CatalogCreate, CatalogUpdate, CatalogDelete, CatalogImport, CatalogExport,
        ContentView, ContentUpdate,
        BlogView, BlogCreate, BlogUpdate, BlogDelete,
        OrdersView, OrdersUpdate, OrdersConfirmPayment, OrdersCancel, OrdersPrint,
        SettingsView, SettingsUpdate, SettingsPayment,
        SiteView, SiteUpdate,
        RoutesView, RoutesUpdate, RoutesRelease,
        ReviewsView, ReviewsModerate,
        TranslationsView, TranslationsUpdate,
        UserManage,
    ];

    /// <summary>
    /// Every permission string a role could have held before this granular set existed, mapped to what it expands
    /// to now. <c>PermissionMigration</c> uses this once, at the first start after the change, to give every role
    /// that held the coarse permission all of its replacements.
    /// </summary>
    public static IReadOnlyDictionary<string, string[]> PermissionsReplacedByManage { get; } = new Dictionary<string, string[]>
    {
        ["Catalog.Manage"] = [CatalogView, CatalogCreate, CatalogUpdate, CatalogDelete, CatalogImport, CatalogExport],
        ["Content.Manage"] = [ContentView, ContentUpdate],
        ["Blog.Manage"] = [BlogView, BlogCreate, BlogUpdate, BlogDelete],
        ["Orders.Manage"] = [OrdersView, OrdersUpdate, OrdersConfirmPayment, OrdersCancel, OrdersPrint],
        ["Settings.Manage"] = [SettingsView, SettingsUpdate, SettingsPayment],
        ["Site.Manage"] = [SiteView, SiteUpdate],
        ["Routes.Manage"] = [RoutesView, RoutesUpdate, RoutesRelease],
        ["Reviews.Manage"] = [ReviewsView, ReviewsModerate],
        ["Translations.Manage"] = [TranslationsView, TranslationsUpdate],
    };
}
