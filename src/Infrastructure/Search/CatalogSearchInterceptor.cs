using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Shop.Application.Search;
using Shop.Domain.Catalog;

namespace Shop.Infrastructure.Search;

/// <summary>A save that touches a product, variant, image, category or brand makes the search index rebuild on its next query (no query here, no transaction).</summary>
public sealed class CatalogSearchInterceptor(IServiceProvider services) : SaveChangesInterceptor
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
        if (context is not null && context.ChangeTracker.Entries().Any(e =>
                e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                && e.Entity is Product or ProductVariant or ProductImage or Category or Brand))
        {
            _pending.AddOrUpdate(context, true);
        }
    }

    private void Notify(DbContext? context)
    {
        if (context is not null && _pending.Remove(context))
        {
            services.GetRequiredService<ICatalogSearch>().MarkChanged();
        }
    }

    private void Forget(DbContext? context)
    {
        if (context is not null)
        {
            _pending.Remove(context);
        }
    }
}
