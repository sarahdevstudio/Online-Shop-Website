using System.Text.Json;
using Shop.Application.Common;
using Shop.Domain.Site;

namespace Shop.Application.Site;

// ---------------------------------------------------------------- admin

public sealed record SiteOverviewItemDto(string Kind, bool HasDraft, DateTime? DraftUpdatedAtUtc, int? PublishedVersion, DateTime? PublishedAtUtc);

public sealed record SiteVersionInfoDto(int Version, string State, DateTime? PublishedAtUtc, string? PublishedBy);

public sealed record SitePublishedDto(int Version, JsonElement Payload, DateTime PublishedAtUtc, string? PublishedBy);

/// <summary><see cref="RowVersion"/> is the value to send back with the next save, publish or discard.</summary>
public sealed record SiteDraftDto(JsonElement Payload, byte[] RowVersion, DateTime UpdatedAtUtc, string? UpdatedBy);

public sealed record SiteMediaDto(string Url, int Width, int Height, string Purpose);

public sealed record SiteReportDto(IReadOnlyList<ContrastResult> Contrast);

public sealed record SiteKindDto(
    string Kind,
    SitePublishedDto? Published,
    SiteDraftDto? Draft,
    IReadOnlyList<SiteVersionInfoDto> History,
    IReadOnlyDictionary<string, SiteMediaDto> Media,
    SiteReportDto? Report);

public sealed record SiteVersionDto(int Version, string State, JsonElement Payload, DateTime? PublishedAtUtc, string? PublishedBy);

/// <summary><see cref="RowVersion"/> is null when no draft exists yet (the save creates it).</summary>
public sealed record SaveSiteDraftRequest(JsonElement Payload, byte[]? RowVersion);

public sealed record PublishSiteRequest(byte[] RowVersion);

public sealed record DiscardSiteDraftRequest(byte[] RowVersion);

public sealed record RestoreSiteVersionRequest(bool Publish);

public sealed record SiteImageUpload(Stream Content, string FileName, string ContentType, long Length, string? Alt, string Purpose);

public sealed record SiteImageDto(int Id, string Url, int Width, int Height, long Bytes, string Purpose, IReadOnlyList<string> Warnings);

public sealed record SiteAuditEntryDto(DateTime AtUtc, string Kind, string Action, int? Version, string Summary, string? AdminUsername);

public sealed record SiteAuditPageDto(IReadOnlyList<SiteAuditEntryDto> Items, int Total, int Page, int PageCount);

// ---------------------------------------------------------------- compiled document (what the storefront reads)

public sealed record SiteImageRefDto(string Url, int Width, int Height);

public sealed record SiteLogoDto(string Url, string Alt, int Width, int Height);

public sealed record SiteThemeDto(
    string Primary, string Secondary, string Accent, string Background, string Surface, string Text,
    string Success, string Warning, string Danger, string Font, string Radius, SiteLogoDto? Logo, SiteImageRefDto? Favicon);

public sealed record SiteBlockDto(Guid Id, string Type, object Data);

public sealed record SiteHeroSlideDto(SiteImageRefDto? Image, string Alt, string Title, string Text, string LinkLabel, string? Link);

public sealed record SiteHeroDataDto(int AutoplaySeconds, IReadOnlyList<SiteHeroSlideDto> Slides);

public sealed record SiteCategoryTileDto(string Label, string Slug, string? ImageUrl);

public sealed record SiteCategoryTilesDataDto(IReadOnlyList<SiteCategoryTileDto> Items);

public sealed record SiteProductRowDataDto(string Title, string Source, int Limit, string? CategorySlug, IReadOnlyList<string> ProductSlugs, string? ViewAllHref);

public sealed record SiteTextDataDto(string Heading, string Body);

public sealed record SiteBannerDataDto(SiteImageRefDto Image, string Alt, string? Link, string Height);

public sealed record SiteHomeDto(IReadOnlyList<SiteBlockDto> Blocks);

public sealed record SiteChromeDto(
    AnnouncementBar Announcement, IReadOnlyList<MenuItem> HeaderMenu, FooterContent Footer, IReadOnlyDictionary<string, string> Social);

public sealed record SiteSeoDto(string SiteName, string TitleTemplate, string HomeTitle, string DefaultDescription, SiteImageRefDto? DefaultOgImage);

public sealed record SiteConfigDto(string Version, SiteThemeDto Theme, SiteHomeDto Home, SiteChromeDto Chrome, SiteSeoDto Seo, BadgesPayload Badges);

// ---------------------------------------------------------------- services

/// <summary>The published configuration for the public site. Cached in memory; publish and restore invalidate it.</summary>
public interface ISiteConfigService
{
    Task<SiteConfigDto> GetPublishedAsync(CancellationToken ct);

    void Invalidate();
}

public interface IAdminSiteService
{
    Task<IReadOnlyList<SiteOverviewItemDto>> OverviewAsync(CancellationToken ct);

    Task<Result<SiteKindDto>> GetAsync(string kind, CancellationToken ct);

    Task<Result<SiteKindDto>> SaveDraftAsync(string kind, SaveSiteDraftRequest request, CancellationToken ct);

    Task<Result<SiteKindDto>> PublishAsync(string kind, PublishSiteRequest request, CancellationToken ct);

    Task<Result<SiteKindDto>> DiscardAsync(string kind, DiscardSiteDraftRequest request, CancellationToken ct);

    Task<Result<SiteKindDto>> RestoreAsync(string kind, int version, RestoreSiteVersionRequest request, CancellationToken ct);

    Task<Result<SiteVersionDto>> GetVersionAsync(string kind, int version, CancellationToken ct);

    Task<Result<SiteImageDto>> UploadImageAsync(SiteImageUpload upload, CancellationToken ct);

    Task<Result<SiteAuditPageDto>> AuditAsync(string? kind, int page, CancellationToken ct);

    /// <summary>The compiled document with drafts applied (a kind without a draft uses its published version). For preview only.</summary>
    Task<SiteConfigDto> CompiledDraftAsync(CancellationToken ct);
}
