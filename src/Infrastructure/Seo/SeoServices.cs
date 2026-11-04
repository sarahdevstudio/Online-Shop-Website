using Microsoft.EntityFrameworkCore;
using Shop.Application.Catalog;
using Shop.Application.Common;
using Shop.Application.Seo;
using Shop.Domain.Catalog;
using Shop.Domain.Common;
using Shop.Domain.Routing;
using Shop.Domain.Seo;
using Shop.Infrastructure.Persistence;

namespace Shop.Infrastructure.Seo;

/// <summary>Per-entity SEO fields, FAQ and category guide (admin) and what the storefront reads (public).</summary>
internal sealed class SeoService(ShopDbContext db, ICurrentUser currentUser, TimeProvider clock, IMediaStorage media) : IAdminSeoService, IPublicSeoService
{
    private static readonly Error NotFound = Error.NotFound("seo_target_not_found", "api.error.requested-item-not-found");
    private static readonly Error Stale = Error.Conflict("concurrent_update", "api.error.seo-concurrent-update");

    private static bool SupportsFaq(string type) => type is RouteTargetTypes.Product or RouteTargetTypes.Category;

    // ---- admin -----------------------------------------------------------------------------------------------------------

    public async Task<Result<AdminSeoDto>> GetAsync(string type, int id, CancellationToken ct)
    {
        var target = await FindByIdAsync(type, id, ct);
        if (target is null)
        {
            return NotFound;
        }

        var meta = await db.SeoMetas.AsNoTracking().SingleOrDefaultAsync(m => m.TargetType == type && m.TargetId == id, ct);
        var faq = SupportsFaq(type) ? await LoadFaqAsync(type, id, ct) : [];
        return new AdminSeoDto(
            type, id, target.Name, await PathOfAsync(type, id, ct), new SeoFieldsDto(meta?.MetaTitle, meta?.MetaDescription, meta?.OgMediaId, await OgUrlAsync(meta?.OgMediaId, ct), meta?.NoIndex ?? false),
            faq, target.Intro, SupportsFaq(type), type == RouteTargetTypes.Category, meta?.RowVersion);
    }

    public async Task<Result<AdminSeoDto>> SaveAsync(string type, int id, SaveSeoRequest request, CancellationToken ct)
    {
        var target = await FindByIdAsync(type, id, ct);
        if (target is null)
        {
            return NotFound;
        }

        var errors = new Dictionary<string, string[]>();
        var title = Clean(request.MetaTitle);
        var description = Clean(request.MetaDescription);
        if (title is { Length: > SeoMeta.TitleMaxLength })
        {
            errors["metaTitle"] = [Msg.Of("api.validation.text-max-length", ("max", SeoMeta.TitleMaxLength))];
        }

        if (description is { Length: > SeoMeta.DescriptionMaxLength })
        {
            errors["metaDescription"] = [Msg.Of("api.validation.text-max-length", ("max", SeoMeta.DescriptionMaxLength))];
        }

        int? og = request.OgMediaId is > 0 ? request.OgMediaId : null;
        if (og is not null && !await db.SiteMedia.AnyAsync(m => m.Id == og, ct))
        {
            errors["ogMediaId"] = ["api.validation.seo-og-media"];
        }

        var faq = (request.Faq ?? []).Select(f => new FaqItemDto(PersianText.Normalize(f.Question), PersianText.NormalizeLetters(f.Answer))).ToList();
        if (!SupportsFaq(type) && faq.Count > 0)
        {
            errors["faq"] = ["api.validation.seo-faq-unsupported"];
        }
        else if (faq.Count > FaqItem.MaxPerTarget)
        {
            errors["faq"] = [Msg.Of("api.validation.seo-faq-count", ("max", FaqItem.MaxPerTarget))];
        }
        else
        {
            for (var i = 0; i < faq.Count; i++)
            {
                if (faq[i].Question.Length is 0 or > FaqItem.QuestionMaxLength)
                {
                    errors[$"faq[{i}].question"] = [Msg.Of("api.validation.required-max-length", ("maxLength", FaqItem.QuestionMaxLength))];
                }

                if (faq[i].Answer.Length is 0 or > FaqItem.AnswerMaxLength)
                {
                    errors[$"faq[{i}].answer"] = [Msg.Of("api.validation.required-max-length", ("maxLength", FaqItem.AnswerMaxLength))];
                }
            }
        }

        var intro = string.IsNullOrWhiteSpace(request.Intro) ? null : request.Intro.Trim();
        if (type != RouteTargetTypes.Category && intro is not null)
        {
            errors["intro"] = ["api.validation.seo-intro-unsupported"];
        }
        else if (intro is { Length: > Category.IntroMaxLength })
        {
            errors["intro"] = [Msg.Of("api.validation.text-max-length", ("max", Category.IntroMaxLength))];
        }

        if (errors.Count > 0)
        {
            return Error.Validation(errors);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var meta = await db.SeoMetas.SingleOrDefaultAsync(m => m.TargetType == type && m.TargetId == id, ct);
        if (meta is null)
        {
            if (request.RowVersion is { Length: > 0 })
            {
                return Stale; // the row was removed meanwhile
            }

            meta = SeoMeta.Create(type, id);
            db.SeoMetas.Add(meta);
        }
        else
        {
            db.Entry(meta).Property(m => m.RowVersion).OriginalValue = request.RowVersion ?? [];
        }

        meta.Apply(title, description, og, request.NoIndex, currentUser.UserId, now);

        if (SupportsFaq(type))
        {
            var old = await db.FaqItems.Where(f => f.TargetType == type && f.TargetId == id).ToListAsync(ct);
            db.FaqItems.RemoveRange(old);
            db.FaqItems.AddRange(faq.Select((f, i) => FaqItem.Create(type, id, f.Question, f.Answer, i)));
        }

        if (type == RouteTargetTypes.Category)
        {
            var category = await db.Categories.SingleAsync(c => c.Id == id, ct);
            category.SetIntro(intro);
        }

        try
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Stale;
        }
        catch (DbUpdateException)
        {
            return Stale; // two first saves raced on the unique (type, id)
        }

        return await GetAsync(type, id, ct);
    }

    // ---- public -----------------------------------------------------------------------------------------------------------

    public async Task<Result<PublicSeoDto>> GetAsync(string type, string slug, CancellationToken ct)
    {
        var id = await IdBySlugAsync(type, slug, ct);
        if (id is null)
        {
            return NotFound;
        }

        var meta = await db.SeoMetas.AsNoTracking().SingleOrDefaultAsync(m => m.TargetType == type && m.TargetId == id, ct);
        var faq = SupportsFaq(type) ? await LoadFaqAsync(type, id.Value, ct) : [];
        string? intro = type == RouteTargetTypes.Category ? await db.Categories.AsNoTracking().Where(c => c.Id == id).Select(c => c.IntroMarkdown).SingleAsync(ct) : null;
        return new PublicSeoDto(meta?.MetaTitle, meta?.MetaDescription, await OgUrlAsync(meta?.OgMediaId, ct), meta?.NoIndex ?? false, faq, intro);
    }

    public async Task<NoIndexDto> GetNoIndexAsync(CancellationToken ct)
    {
        var rows = await db.SeoMetas.AsNoTracking().Where(m => m.NoIndex).Select(m => new { m.TargetType, m.TargetId }).ToListAsync(ct);
        async Task<List<string>> Slugs(string type)
        {
            var ids = rows.Where(r => r.TargetType == type).Select(r => r.TargetId).ToList();
            if (ids.Count == 0)
            {
                return [];
            }

            return type switch
            {
                RouteTargetTypes.Product => await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).Select(p => p.Slug).ToListAsync(ct),
                RouteTargetTypes.Category => await db.Categories.AsNoTracking().Where(c => ids.Contains(c.Id)).Select(c => c.Slug).ToListAsync(ct),
                RouteTargetTypes.ContentPage => await db.ContentPages.AsNoTracking().Where(c => ids.Contains(c.Id)).Select(c => c.Slug).ToListAsync(ct),
                _ => await db.BlogPosts.AsNoTracking().Where(b => ids.Contains(b.Id)).Select(b => b.Slug).ToListAsync(ct),
            };
        }

        return new NoIndexDto(await Slugs(RouteTargetTypes.Product), await Slugs(RouteTargetTypes.Category), await Slugs(RouteTargetTypes.ContentPage), await Slugs(RouteTargetTypes.BlogPost));
    }

    // ---- helpers ------------------------------------------------------------------------------------------------------------

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : PersianText.Normalize(value);

    private sealed record Target(string Name, string? Intro);

    private async Task<Target?> FindByIdAsync(string type, int id, CancellationToken ct) => type switch
    {
        RouteTargetTypes.Product => await db.Products.AsNoTracking().Where(p => p.Id == id).Select(p => new Target(p.Name, null)).SingleOrDefaultAsync(ct),
        RouteTargetTypes.Category => await db.Categories.AsNoTracking().Where(c => c.Id == id).Select(c => new Target(c.Name, c.IntroMarkdown)).SingleOrDefaultAsync(ct),
        RouteTargetTypes.ContentPage => await db.ContentPages.AsNoTracking().Where(c => c.Id == id).Select(c => new Target(c.Title, null)).SingleOrDefaultAsync(ct),
        RouteTargetTypes.BlogPost => await db.BlogPosts.AsNoTracking().Where(b => b.Id == id).Select(b => new Target(b.Title, null)).SingleOrDefaultAsync(ct),
        _ => null,
    };

    private async Task<int?> IdBySlugAsync(string type, string slug, CancellationToken ct) => type switch
    {
        RouteTargetTypes.Product => await db.Products.AsNoTracking().Where(p => p.Slug == slug).Select(p => (int?)p.Id).SingleOrDefaultAsync(ct),
        RouteTargetTypes.Category => await db.Categories.AsNoTracking().Where(c => c.Slug == slug).Select(c => (int?)c.Id).SingleOrDefaultAsync(ct),
        RouteTargetTypes.ContentPage => await db.ContentPages.AsNoTracking().Where(c => c.Slug == slug).Select(c => (int?)c.Id).SingleOrDefaultAsync(ct),
        RouteTargetTypes.BlogPost => await db.BlogPosts.AsNoTracking().Where(b => b.Slug == slug).Select(b => (int?)b.Id).SingleOrDefaultAsync(ct),
        _ => null,
    };

    private async Task<List<FaqItemDto>> LoadFaqAsync(string type, int id, CancellationToken ct) =>
        await db.FaqItems.AsNoTracking().Where(f => f.TargetType == type && f.TargetId == id).OrderBy(f => f.SortOrder).ThenBy(f => f.Id)
            .Select(f => new FaqItemDto(f.Question, f.Answer)).ToListAsync(ct);

    private async Task<string> PathOfAsync(string type, int id, CancellationToken ct) =>
        await db.RoutePaths.AsNoTracking().Where(r => r.TargetType == type && r.TargetId == id && r.IsCurrent && !r.IsGone).Select(r => r.Path).FirstOrDefaultAsync(ct) ?? string.Empty;

    private async Task<string?> OgUrlAsync(int? mediaId, CancellationToken ct)
    {
        if (mediaId is null)
        {
            return null;
        }

        var path = await db.SiteMedia.AsNoTracking().Where(m => m.Id == mediaId).Select(m => m.Path).SingleOrDefaultAsync(ct);
        return path is null ? null : media.GetUrl(path);
    }
}
