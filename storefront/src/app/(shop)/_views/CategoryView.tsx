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
import { seoDefaults } from '@/lib/site-config';

/** `path` is the category's current public path (the listing's base: filters, sort and paging are appended to it). */
type Props = { slug: string; path: string; searchParams: SearchParams };

/** Option slugs that exist in the category, from the unfiltered listing's facets. */
function allowedOptions(facets: FacetsDto): AllowedOptions {
  const slugs = (code: string) => new Set(facets.attributes.find((a) => a.code === code)?.options.map((o) => o.slug) ?? []);
  return { brand: new Set(facets.brands.map((b) => b.slug)), wattage: slugs('wattage'), base: slugs('base'), cct: slugs('cct') };
}

/**
 * Resolves category + listing, or null for a 404 (unknown category, invalid or out-of-range page).
 * A URL that is not in normalized form (key order, sorted values, invalid keys/values, page=1) 308-redirects to it.
 */
async function load({ slug, path, searchParams: query }: Props) {
  const category = await getCategory(slug);
  if (!category) return null;

  // The unfiltered first page: what the category contains (valid filter values, labels) and its clean listing.
  const all = await getProducts({ category: slug });
  if (!all) return null;

  const basePath = path;
  const { state, invalidPage } = parseListing(query, allowedOptions(all.facets));
  if (invalidPage) return null;
  if (serializeRequest(query) !== serializeListing(state)) permanentRedirect(listingHref(basePath, state));

  const result = serializeListing(state) === '' ? all : await getProducts({ category: slug, listing: state });
  if (!result || !isPageInRange(state.page, result.pageCount)) return null;
  return { slug, basePath, category, state, result, allFacets: all.facets };
}

export async function categoryMetadata(props: Props): Promise<Metadata> {
  const data = await load(props);
  if (!data) return {};
  const { basePath, category, state, result } = data;
  const defaults = await seoDefaults();
  return pageMetadata({
    siteName: defaults.siteName,
    image: defaults.image,
    title: result.page > 1 ? messages.listingPageTitle(category.name, formatNumber(result.page)) : category.name,
    description: category.description || category.name,
    path: listingHref(basePath, state),
    ...listingIndexing(state),
  });
}

export async function CategoryView(props: Props) {
  const data = await load(props);
  if (!data) notFound();
  const { basePath, category, state, result, allFacets } = data;
  const linkQuery = productLinkQuery(state);

  return (
    <div className="ui-container ui-section ui-stack">
      <Breadcrumb
        items={[
          { label: messages.home, href: '/' },
          ...(category.parent ? [{ label: category.parent.name, href: category.parent.path }] : []),
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
                <Link href={child.path}>{child.name}</Link>
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
              <h2 className="ui-visually-hidden">{messages.productsHeading}</h2>
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
