import { Pagination } from '@/components/Pagination';
import { ProductCard } from '@/components/ProductCard';
import { notFound, permanentRedirect } from 'next/navigation';
import { getProducts } from '@/lib/catalog';
import { isPageInRange, pageOneRedirect, parsePageParam, type SearchParams } from '@/lib/paging';
import { messages } from '@/lib/messages';
import { pageMetadata } from '@/lib/seo';

export const metadata = pageMetadata({ title: 'جستجو', description: 'جستجو در محصولات', path: '/search', noindex: true });

export default async function SearchPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const query = await searchParams;
  const redirectTo = pageOneRedirect('/search', query);
  if (redirectTo) permanentRedirect(redirectTo);

  const q = typeof query.q === 'string' ? query.q : '';
  const pageNumber = typeof query.page === 'string' || query.page === undefined ? parsePageParam(query.page) : null;
  if (pageNumber === null) notFound();
  const result = await getProducts({ q, page: pageNumber });
  if (!isPageInRange(pageNumber, result.pageCount)) notFound();

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
