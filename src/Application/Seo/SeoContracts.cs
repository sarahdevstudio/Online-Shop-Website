using Shop.Application.Common;

namespace Shop.Application.Seo;

/// <summary>The SEO fields of one entity, as the admin edits them. Empty title / description = the generated default.</summary>
public sealed record SeoFieldsDto(string? MetaTitle, string? MetaDescription, int? OgMediaId, string? OgImageUrl, bool NoIndex);

public sealed record FaqItemDto(string Question, string Answer);

/// <summary>
/// Everything the admin edits about one entity's search presence (docs/17): SEO fields, the FAQ (products and categories) and the
/// category buying guide. <see cref="RowVersion"/> is null until the first save; a save with an old one is a 409.
/// </summary>
public sealed record AdminSeoDto(
    string Type, int Id, string Name, string Path, SeoFieldsDto Seo, IReadOnlyList<FaqItemDto> Faq, string? Intro,
    bool SupportsFaq, bool SupportsIntro, byte[]? RowVersion);

public sealed record SaveSeoRequest(
    string? MetaTitle, string? MetaDescription, int? OgMediaId, bool NoIndex, IReadOnlyList<FaqItemDto>? Faq, string? Intro, byte[]? RowVersion);

/// <summary>What the storefront reads for one public entity: overrides, FAQ and (category) the guide.</summary>
public sealed record PublicSeoDto(string? Title, string? Description, string? OgImageUrl, bool NoIndex, IReadOnlyList<FaqItemDto> Faq, string? Intro);

/// <summary>Slugs of the entities that asked not to be indexed, by type: the storefront leaves them out of the sitemaps.</summary>
public sealed record NoIndexDto(IReadOnlyList<string> Product, IReadOnlyList<string> Category, IReadOnlyList<string> ContentPage, IReadOnlyList<string> BlogPost);

public interface IAdminSeoService
{
    Task<Result<AdminSeoDto>> GetAsync(string type, int id, CancellationToken ct);

    Task<Result<AdminSeoDto>> SaveAsync(string type, int id, SaveSeoRequest request, CancellationToken ct);
}

public interface IPublicSeoService
{
    /// <summary>Empty overrides (not a 404) for an entity without a row; 404 only for an entity that does not exist.</summary>
    Task<Result<PublicSeoDto>> GetAsync(string type, string slug, CancellationToken ct);

    Task<NoIndexDto> GetNoIndexAsync(CancellationToken ct);
}
