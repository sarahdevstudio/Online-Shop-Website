import { Pagination } from '@/components/Pagination';
import { ProductCard } from '@/components/ProductCard';
import { getProducts } from '@/lib/catalog';
import { messages } from '@/lib/messages';
import { pageMetadata } from '@/lib/seo';

export const metadata = pageMetadata({ title: 'جستجو', description: 'جستجو در محصولات', path: '/search', noindex: true });

export default async function SearchPage({ searchParams }: { searchParams: Promise<{ q?: string; page?: string }> }) {
  const { q = '', page } = await searchParams;
  const result = await getProducts({ q, page: Number(page) || 1 });

  return (
    <div className="ui-container ui-section ui-stack">
      <h1 className="ui-heading-2">جستجو{q && `: ${q}`}</h1>
      {result.items.length === 0 ? (
        <p>{messages.emptyTitle}</p>
      ) : (
        <div className="ui-product-grid">
          {result.items.map((p) => (
            <ProductCard key={p.slug} product={p} />
          ))}
        </div>
      )}
      <Pagination page={result.page} pageCount={result.pageCount} basePath="/search" query={q ? { q } : {}} />
    </div>
  );
}
