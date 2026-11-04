namespace Shop.Domain.Site;

/// <summary>
/// One stored version of one kind of site configuration. At most one draft and one published row exist per kind;
/// archived rows are immutable history (the last <see cref="KeptVersions"/> are kept).
/// </summary>
public sealed class SiteConfigVersion
{
    public const int KeptVersions = 20;
    public const int PayloadMaxLength = 400_000;

    private SiteConfigVersion()
    {
        Payload = string.Empty;
    }

    public int Id { get; private set; }
    public SiteKind Kind { get; private set; }

    /// <summary>Null for a draft; assigned (1, 2, 3, ...) when published.</summary>
    public int? Version { get; private set; }

    public SiteState State { get; private set; }
    public string Payload { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }
    public Guid? PublishedByUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static SiteConfigVersion NewDraft(SiteKind kind, string payload, Guid? userId, DateTime utcNow) => new()
    {
        Kind = kind,
        State = SiteState.Draft,
        Payload = payload,
        CreatedAtUtc = utcNow,
        CreatedByUserId = userId,
        UpdatedAtUtc = utcNow,
        UpdatedByUserId = userId,
    };

    /// <summary>A version published on creation (the seed).</summary>
    public static SiteConfigVersion NewPublished(SiteKind kind, int version, string payload, Guid? userId, DateTime utcNow)
    {
        var row = NewDraft(kind, payload, userId, utcNow);
        row.Publish(version, userId, utcNow);
        return row;
    }

    public void SetPayload(string payload, Guid? userId, DateTime utcNow)
    {
        if (State != SiteState.Draft)
        {
            throw new InvalidOperationException("Only a draft can be edited; published and archived versions are immutable.");
        }

        Payload = payload;
        UpdatedAtUtc = utcNow;
        UpdatedByUserId = userId;
    }

    /// <summary>
    /// Rewrites the payload of ANY state, for the one-time conversion of typed links to entity links (the content stays the
    /// same: a link and its entity lead to the same page). Nothing else may change a published or archived payload.
    /// </summary>
    public void ReplacePayloadForConversion(string payload) => Payload = payload;

    public void Publish(int version, Guid? userId, DateTime utcNow)
    {
        if (State != SiteState.Draft)
        {
            throw new InvalidOperationException("Only a draft can be published.");
        }

        State = SiteState.Published;
        Version = version;
        PublishedAtUtc = utcNow;
        PublishedByUserId = userId;
    }

    public void Archive()
    {
        if (State != SiteState.Published)
        {
            throw new InvalidOperationException("Only the published version can be archived.");
        }

        State = SiteState.Archived;
    }
}

/// <summary>An uploaded site image (logo, favicon, hero slide, banner, social image). Referenced by id from payloads.</summary>
public sealed class SiteMedia
{
    public const int AltMaxLength = 150;

    private SiteMedia()
    {
        Path = ContentType = Purpose = string.Empty;
    }

    public int Id { get; private set; }
    public string Path { get; private set; }
    public string ContentType { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public long Bytes { get; private set; }
    public string Purpose { get; private set; }
    public DateTime UploadedAtUtc { get; private set; }
    public Guid? UploadedByUserId { get; private set; }

    public static SiteMedia Create(string path, string contentType, int width, int height, long bytes, string purpose, Guid? userId, DateTime utcNow) => new()
    {
        Path = path,
        ContentType = contentType,
        Width = width,
        Height = height,
        Bytes = bytes,
        Purpose = purpose,
        UploadedAtUtc = utcNow,
        UploadedByUserId = userId,
    };
}

/// <summary>Who did what to which kind of configuration. The summary lists field names only, never payload text.</summary>
public sealed class SiteAuditLog
{
    public const int SummaryMaxLength = 500;

    private SiteAuditLog()
    {
        Summary = string.Empty;
    }

    public int Id { get; private set; }
    public SiteKind Kind { get; private set; }
    public SiteAuditAction Action { get; private set; }
    public int? Version { get; private set; }
    public string Summary { get; private set; }
    public Guid? AdminUserId { get; private set; }
    public DateTime AtUtc { get; private set; }

    public static SiteAuditLog Create(SiteKind kind, SiteAuditAction action, int? version, string summary, Guid? adminUserId, DateTime utcNow) => new()
    {
        Kind = kind,
        Action = action,
        Version = version,
        Summary = summary.Length > SummaryMaxLength ? summary[..SummaryMaxLength] : summary,
        AdminUserId = adminUserId,
        AtUtc = utcNow,
    };
}
