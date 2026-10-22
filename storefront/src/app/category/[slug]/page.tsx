import type { Metadata } from 'next';
import Link from 'next/link';
import { notFound, permanentRedirect } from 'next/navigation';
import { Breadcrumb } from '@/components/Breadcrumb';
import { Pagination } from '@/components/Pagination';
import { ProductCard } from '@/components/ProductCard';
import { formatNumber } from '@/lib/format';
import { getCategory, getProducts } from '@/lib/catalog';
import { messages } from '@/lib/messages';
import { isPageInRange, pageOneRedirect, parsePageParam, type SearchParams } from '@/lib/paging';
import { pageMetadata } from '@/lib/seo';

type Props = {
  params: Promise<{ slug: string }>;
  searchParams: Promise<SearchParams>;
};

// No generateStaticParams: categories render on first request (the build never needs the API).

/**
 * Resolves category + listing for the request, or null when the URL has no valid page to show (404).
 * `?page=1` on an existing category redirects (308) to the clean URL.
 */
async function load({ params, searchParams }: Props) {
  const { slug } = await params;
  const query = await searchParams;
  const category = await getCategory(slug);
  if (!category) return null;

  const redirectTo = pageOneRedirect(`/category/${slug}`, query);
  if (redirectTo) permanentRedirect(redirectTo);

  const page = typeof query.page === 'string' || query.page === undefined ? parsePageParam(query.page) : null;
  if (page === null) return null;

  const result = await getProducts({ category: slug, page });
  return result && isPageInRange(page, result.pageCount) ? { slug, category, result } : null;
}

export async function generateMetadata(props: Props): Promise<Metadata> {
  const data = await load(props);
  if (!data) return {};
  const { slug, category, result } = data;
  // Page 1 and ?page=1 share one canonical URL; deeper pages canonicalize to themselves.
  return pageMetadata({
    title: result.page > 1 ? `${category.name} - صفحه ${formatNumber(result.page)}` : category.name,
    description: category.description || category.name,
    path: `/category/${slug}${result.page > 1 ? `?page=${result.page}` : ''}`,
  });
}

export default async function CategoryPage(props: Props) {
  const data = await load(props);
  if (!data) notFound();
  const { slug, category, result } = data;

  return (
    <div className="ui-container ui-section ui-stack">
      <Breadcrumb
        items={[
          { label: messages.home, href: '/' },
          ...(category.parent ? [{ label: category.parent.name, href: `/category/${category.parent.slug}` }] : []),
          { label: category.name },
        ]}
      />
      <h1 className="ui-heading-2">{category.name}</h1>
      {category.description && <p className="ui-text-muted">{category.description}</p>}
      {category.children.length > 0 && (
        <nav aria-label={messages.subcategories}>
          <ul className="ui-cluster">
            {category.children.map((child) => (
              <li key={child.slug}>
                <Link href={`/category/${child.slug}`}>{child.name}</Link>
              </li>
            ))}
          </ul>
        </nav>
      )}
      {result.items.length === 0 ? (
        <p>{messages.emptyTitle}</p>
      ) : (
        <div className="ui-product-grid">
          {result.items.map((p, i) => (
            <ProductCard key={p.slug} product={p} priority={i < 4} />
          ))}
        </div>
      )}
      <Pagination page={result.page} pageCount={result.pageCount} basePath={`/category/${slug}`} />
    </div>
  );
}
