namespace Shop.Domain.Security;

/// <summary>
/// One row per change to staff accounts and roles (who did what to whom). The detail names fields, roles and permissions,
/// never passwords or tokens.
/// </summary>
public sealed class StaffAuditLog
{
    public const int ActionMaxLength = 40;
    public const int NameMaxLength = 256;
    public const int DetailMaxLength = 1000;

    private StaffAuditLog()
    {
        Action = string.Empty;
    }

    public int Id { get; private set; }
    public DateTime AtUtc { get; private set; }
    public Guid? ActorUserId { get; private set; }

    /// <summary>The staff account or the role the change was about (its name at that time).</summary>
    public string? Subject { get; private set; }
    public string Action { get; private set; }
    public string? Detail { get; private set; }

    public static StaffAuditLog Create(Guid? actorUserId, string action, string? subject, string? detail, DateTime utcNow) => new()
    {
        AtUtc = utcNow,
        ActorUserId = actorUserId,
        Action = action,
        Subject = subject is { Length: > NameMaxLength } ? subject[..NameMaxLength] : subject,
        Detail = detail is { Length: > DetailMaxLength } ? detail[..DetailMaxLength] : detail,
    };
}

/// <summary>The audit actions staff management writes.</summary>
public static class StaffAuditActions
{
    public const string Created = "staff.created";
    public const string Updated = "staff.updated";
    public const string RolesChanged = "staff.roles-changed";
    public const string Deactivated = "staff.deactivated";
    public const string Activated = "staff.activated";
    public const string PasswordReset = "staff.password-reset";
    public const string ReauthFailed = "staff.reauth-failed";
    public const string RoleCreated = "role.created";
    public const string RoleUpdated = "role.updated";
    public const string RoleDeleted = "role.deleted";
}
