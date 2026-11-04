namespace Shop.Domain.Routing;

/// <summary>
/// One public path of one entity. Exactly one row per entity is current; its older paths stay as non-current rows that point at
/// the SAME entity, so an old URL is always one hop from the current one (no chains, no loops by construction). A row of a
/// hard-deleted entity is kept as <see cref="IsGone"/> so the path answers 410 and is not silently reused.
/// </summary>
public sealed class RouteEntry
{
    public const int MaxOldPathsPerTarget = 30;

    private RouteEntry()
    {
        Path = TargetType = string.Empty;
    }

    public int Id { get; private set; }
    public string Path { get; private set; }
    public string TargetType { get; private set; }
    public int TargetId { get; private set; }
    public bool IsCurrent { get; private set; }
    public bool IsGone { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static RouteEntry Create(string path, string targetType, int targetId, bool isCurrent, Guid? userId, DateTime utcNow)
    {
        if (!RouteTargetTypes.IsKnown(targetType))
        {
            throw new ArgumentException($"'{targetType}' is not a route target type.", nameof(targetType));
        }

        return new RouteEntry
        {
            Path = path,
            TargetType = targetType,
            TargetId = targetId,
            IsCurrent = isCurrent,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
            CreatedByUserId = userId,
        };
    }

    public void SetCurrent(bool current, DateTime utcNow)
    {
        IsCurrent = current;
        UpdatedAtUtc = utcNow;
    }

    public void MarkGone(DateTime utcNow)
    {
        IsGone = true;
        IsCurrent = false;
        UpdatedAtUtc = utcNow;
    }
}

/// <summary>Who changed which path when. Never edited or deleted.</summary>
public sealed class RouteAuditLog
{
    public const string Created = "created";
    public const string Changed = "changed";
    public const string Released = "released";
    public const string DeletedWithTarget = "deleted-with-target";

    private RouteAuditLog()
    {
        TargetType = Action = string.Empty;
    }

    public int Id { get; private set; }
    public string TargetType { get; private set; }
    public int TargetId { get; private set; }
    public string Action { get; private set; }
    public string? OldPath { get; private set; }
    public string? NewPath { get; private set; }
    public Guid? AdminUserId { get; private set; }
    public DateTime AtUtc { get; private set; }

    public static RouteAuditLog Create(string targetType, int targetId, string action, string? oldPath, string? newPath, Guid? adminUserId, DateTime utcNow) => new()
    {
        TargetType = targetType,
        TargetId = targetId,
        Action = action,
        OldPath = oldPath,
        NewPath = newPath,
        AdminUserId = adminUserId,
        AtUtc = utcNow,
    };
}
