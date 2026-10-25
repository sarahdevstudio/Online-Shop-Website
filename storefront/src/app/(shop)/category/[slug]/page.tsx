import type { Metadata } from 'next';
import Link from 'next/link';
import { notFound, permanentRedirect } from 'next/navigation';
import { Breadcrumb } from '@/components/Breadcrumb';
import { ListingFilters } from '@/components/ListingFilters';
import { ListingToolbar } from '@/components/ListingToolbar';
import { Pagination } from '@/components/Pagination';
import { ProductCard } from '@/components/ProductCard';
import { formatNumber } from '@/lib/format';
import { getCategory, getProducts, type FacetsDto } from '@/lib/catalog';
import {
  clearFilters,
  hasFilters,
  listingHref,
  listingIndexing,
  parseListing,
  productLinkQuery,
  serializeListing,
  serializeRequest,
  withPage,
  type AllowedOptions,
} from '@/lib/listing';
import { messages } from '@/lib/messages';
import { isPageInRange, type SearchParams } from '@/lib/paging';
import { pageMetadata } from '@/lib/seo';

type Props = {
  params: Promise<{ slug: string }>;
  searchParams: Promise<SearchParams>;
};

// No generateStaticParams: categories render on first request (the build never needs the API).

/** Option slugs that exist in the category, from the unfiltered listing's facets. */
function allowedOptions(facets: FacetsDto): AllowedOptions {
  const slugs = (code: string) => new Set(facets.attributes.find((a) => a.code === code)?.options.map((o) => o.slug) ?? []);
  return { brand: new Set(facets.brands.map((b) => b.slug)), wattage: slugs('wattage'), base: slugs('base'), cct: slugs('cct') };
}

/**
 * Resolves category + listing, or null for a 404 (unknown category, invalid or out-of-range page).
 * A URL that is not in normalized form (key order, sorted values, invalid keys/values, page=1) 308-redirects to it.
 */
async function load({ params, searchParams }: Props) {
  const { slug } = await params;
  const query = await searchParams;
  const category = await getCategory(slug);
  if (!category) return null;

  // The unfiltered first page: what the category contains (valid filter values, labels) and its clean listing.
  const all = await getProducts({ category: slug });
  if (!all) return null;

  const basePath = `/category/${slug}`;
  const { state, invalidPage } = parseListing(query, allowedOptions(all.facets));
  if (invalidPage) return null;
  if (serializeRequest(query) !== serializeListing(state)) permanentRedirect(listingHref(basePath, state));

  const result = serializeListing(state) === '' ? all : await getProducts({ category: slug, listing: state });
  if (!result || !isPageInRange(state.page, result.pageCount)) return null;
  return { slug, basePath, category, state, result, allFacets: all.facets };
}

export async function generateMetadata(props: Props): Promise<Metadata> {
  const data = await load(props);
  if (!data) return {};
  const { basePath, category, state, result } = data;
  return pageMetadata({
    title: result.page > 1 ? `${category.name} - صفحه ${formatNumber(result.page)}` : category.name,
    description: category.description || category.name,
    path: listingHref(basePath, state),
    ...listingIndexing(state),
  });
}

export default async function CategoryPage(props: Props) {
  const data = await load(props);
  if (!data) notFound();
  const { basePath, category, state, result, allFacets } = data;
  const linkQuery = productLinkQuery(state);

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

      <div className="ui-listing-layout">
        <aside aria-label={messages.filters}>
          <ListingFilters basePath={basePath} state={state} facets={result.facets} allFacets={allFacets} />
        </aside>

        <div className="ui-stack">
          <ListingToolbar basePath={basePath} state={state} allFacets={allFacets} total={result.total} />
          {result.items.length === 0 ? (
            <div className="ui-stack">
              <p>{hasFilters(state) ? messages.noFilterResults : messages.emptyTitle}</p>
              {hasFilters(state) && (
                <p>
                  <Link href={listingHref(basePath, clearFilters(state))} rel="nofollow">
                    {messages.clearFilters}
                  </Link>
                </p>
              )}
            </div>
          ) : (
            <div className="ui-product-grid ui-product-grid--listing">
              {result.items.map((p, i) => (
                <ProductCard key={p.slug} product={p} linkQuery={linkQuery} priority={i < 4} />
              ))}
            </div>
          )}
          <Pagination
            page={result.page}
            pageCount={result.pageCount}
            basePath={basePath}
            hrefFor={(page) => listingHref(basePath, withPage(state, page))}
          />
        </div>
      </div>
    </div>
  );
}
