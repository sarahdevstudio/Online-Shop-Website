using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Shop.Application.Routing;
using Shop.Domain.Blog;
using Shop.Domain.Catalog;
using Shop.Domain.Content;

namespace Shop.Infrastructure.Site;

/// <summary>
/// A link on the site shows only while its target is published. Publishing or hiding a product, post, info page or category
/// changes no route, so nothing else would tell the cached site document; after such a save this bumps the route version and
/// the document is compiled again on the next read. No query, no transaction involved: the compile itself reads the new state.
/// </summary>
public sealed class LinkStateInterceptor(IServiceProvider services) : SaveChangesInterceptor
{
    private readonly ConditionalWeakTable<DbContext, object> _pending = [];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Note(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Note(eventData.Context);
        return new ValueTask<InterceptionResult<int>>(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Notify(eventData.Context);
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Notify(eventData.Context);
        return new ValueTask<int>(result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => Forget(eventData.Context);

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Forget(eventData.Context);
        return Task.CompletedTask;
    }

    private void Note(DbContext? context)
    {
        if (context is not null && AffectsLinks(context))
        {
            _pending.AddOrUpdate(context, true);
        }
    }

    private void Forget(DbContext? context)
    {
        if (context is not null)
        {
            _pending.Remove(context);
        }
    }

    private void Notify(DbContext? context)
    {
        if (context is not null && _pending.Remove(context))
        {
            services.GetRequiredService<IRouteTable>().MarkChanged();
        }
    }

    private static bool AffectsLinks(DbContext context) =>
        context.ChangeTracker.Entries().Any(e => e.State switch
        {
            EntityState.Modified => e.Entity switch
            {
                Product => e.Property(nameof(Product.Status)).IsModified,
                Category => e.Property(nameof(Category.IsActive)).IsModified,
                BlogPost => e.Property(nameof(BlogPost.IsPublished)).IsModified,
                ContentPage => e.Property(nameof(ContentPage.Body)).IsModified,
                _ => false,
            },
            EntityState.Added or EntityState.Deleted => e.Entity is Product or Category or BlogPost or ContentPage,
            _ => false,
        });
}
