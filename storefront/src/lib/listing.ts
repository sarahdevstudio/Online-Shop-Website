import { parseLocalizedInteger } from './format';
import { parsePageParam, type SearchParams } from './paging';

/**
 * Category listing state ↔ URL. There is exactly one URL per state (the "normalized" form): known keys in a fixed
 * order, list values deduplicated and sorted, invalid keys/values dropped, defaults (page 1, newest) omitted.
 * Pages 308-redirect anything else to that form, so caches and crawlers only ever see one URL per listing.
 */

/** Option filters, in URL order. Values are API slugs; OR within a key, AND across keys (the API's semantics). */
export const OPTION_KEYS = ['brand', 'wattage', 'base', 'cct'] as const;
export type OptionKey = (typeof OPTION_KEYS)[number];

/** Attribute filters a product page understands for variant preselection. */
export const VARIANT_KEYS = ['wattage', 'cct'] as const satisfies readonly OptionKey[];

export const SORTS = ['price-asc', 'price-desc'] as const; // "newest" is the default and never appears in URLs
export type Sort = (typeof SORTS)[number];

export interface ListingState {
  options: Record<OptionKey, string[]>;
  minPrice: number | null;
  maxPrice: number | null;
  inStock: boolean;
  sort: Sort | null;
  page: number;
}

/** Slugs that exist in this category (from the unfiltered listing's facets); anything else is dropped. */
export type AllowedOptions = Record<OptionKey, ReadonlySet<string>>;

export const emptyState = (): ListingState => ({
  options: { brand: [], wattage: [], base: [], cct: [] },
  minPrice: null,
  maxPrice: null,
  inStock: false,
  sort: null,
  page: 1,
});

const KNOWN_KEYS = new Map<string, string>(
  [...OPTION_KEYS, 'minPrice', 'maxPrice', 'inStock', 'sort', 'page'].map((key) => [key.toLowerCase(), key]),
);

const values = (raw: string | string[] | undefined): string[] =>
  (Array.isArray(raw) ? raw : raw === undefined ? [] : [raw])
    .flatMap((v) => v.split(','))
    .map((v) => v.trim().toLowerCase())
    .filter(Boolean);

const price = (raw: string | string[] | undefined): number | null => {
  const value = Array.isArray(raw) ? raw.at(-1) : raw;
  const parsed = value === undefined ? null : parseLocalizedInteger(value);
  return parsed !== null && parsed >= 0 && Number.isSafeInteger(parsed) ? parsed : null;
};

export interface ParsedListing {
  state: ListingState;
  /** `?page=` is present but not a plain positive integer: the page answers 404 (existing paging rule). */
  invalidPage: boolean;
}

/** Reads any query (also non-normalized ones, e.g. from the price form) into a state. */
export function parseListing(params: SearchParams, allowed: AllowedOptions): ParsedListing {
  // Keys are matched case-insensitively; repeated keys (any spelling) are merged.
  const byKey = new Map<string, string[]>();
  for (const [rawKey, raw] of Object.entries(params)) {
    const key = KNOWN_KEYS.get(rawKey.toLowerCase());
    if (!key || raw === undefined) continue;
    byKey.set(key, [...(byKey.get(key) ?? []), ...(Array.isArray(raw) ? raw : [raw])]);
  }

  const state = emptyState();
  for (const key of OPTION_KEYS) {
    state.options[key] = [...new Set(values(byKey.get(key)))].filter((v) => allowed[key].has(v)).sort();
  }

  state.minPrice = price(byKey.get('minPrice'));
  state.maxPrice = price(byKey.get('maxPrice'));
  if (state.minPrice !== null && state.maxPrice !== null && state.minPrice > state.maxPrice) {
    [state.minPrice, state.maxPrice] = [state.maxPrice, state.minPrice];
  }

  const inStock = values(byKey.get('inStock')).at(-1);
  state.inStock = inStock === '1' || inStock === 'true';
  const sort = values(byKey.get('sort')).at(-1);
  state.sort = (SORTS as readonly string[]).includes(sort ?? '') ? (sort as Sort) : null;

  const pageValues = byKey.get('page');
  if (pageValues === undefined) return { state, invalidPage: false };
  const page = pageValues.length === 1 ? parsePageParam(pageValues[0]) : null;
  if (page === null) return { state, invalidPage: true };
  state.page = page;
  return { state, invalidPage: false };
}

/** The normalized query string (without "?"), or "" for the clean category URL. */
export function serializeListing(state: ListingState): string {
  const parts: string[] = [];
  for (const key of OPTION_KEYS) {
    const list = [...new Set(state.options[key])].sort();
    // Literal commas (slugs are URL-safe): `?wattage=12w,9w`, not `12w%2C9w`.
    if (list.length > 0) parts.push(`${key}=${list.map(encodeURIComponent).join(',')}`);
  }
  if (state.minPrice !== null) parts.push(`minPrice=${state.minPrice}`);
  if (state.maxPrice !== null) parts.push(`maxPrice=${state.maxPrice}`);
  if (state.inStock) parts.push('inStock=1');
  if (state.sort) parts.push(`sort=${state.sort}`);
  if (state.page > 1) parts.push(`page=${state.page}`);
  return parts.join('&');
}

/** The request's own query in its original form, comparable with serializeListing to decide on a redirect. */
export function serializeRequest(params: SearchParams): string {
  return Object.entries(params)
    .flatMap(([key, raw]) => (raw === undefined ? [] : (Array.isArray(raw) ? raw : [raw]).map((v) => `${key}=${v}`)))
    .join('&');
}

export const listingHref = (basePath: string, state: ListingState): string => {
  const qs = serializeListing(state);
  return qs ? `${basePath}?${qs}` : basePath;
};

/** Any filter or sort in the URL: such pages are `noindex, follow` and have no canonical. */
export function hasFiltersOrSort(state: ListingState): boolean {
  return (
    OPTION_KEYS.some((key) => state.options[key].length > 0) ||
    state.minPrice !== null ||
    state.maxPrice !== null ||
    state.inStock ||
    state.sort !== null
  );
}

/**
 * Index rules for a listing URL. Clean URL (no params, or only ?page=N): indexable, self-canonical. Any filter or
 * sort: `noindex, follow` and no canonical at all (noindex must not be combined with a cross-page canonical).
 */
export function listingIndexing(state: ListingState): { noindex: 'follow' | undefined; canonical: boolean } {
  const filtered = hasFiltersOrSort(state);
  return { noindex: filtered ? 'follow' : undefined, canonical: !filtered };
}

export function hasFilters(state: ListingState): boolean {
  return hasFiltersOrSort({ ...state, sort: null });
}

// ---- link building: every change resets to page 1 ----

const fresh = (state: ListingState, change: Partial<ListingState>): ListingState => ({
  ...state,
  options: { ...state.options },
  ...change,
  page: 1,
});

export function toggleOption(state: ListingState, key: OptionKey, slug: string): ListingState {
  const current = state.options[key];
  const next = current.includes(slug) ? current.filter((v) => v !== slug) : [...current, slug].sort();
  const result = fresh(state, {});
  result.options[key] = next;
  return result;
}

export const withSort = (state: ListingState, sort: Sort | null): ListingState => fresh(state, { sort });
export const withInStock = (state: ListingState, inStock: boolean): ListingState => fresh(state, { inStock });
export const withoutPrice = (state: ListingState): ListingState => fresh(state, { minPrice: null, maxPrice: null });
export const withPage = (state: ListingState, page: number): ListingState => ({ ...state, page });

/** "Clear all" keeps the chosen sort: it removes filters, not the ordering. */
export const clearFilters = (state: ListingState): ListingState => ({ ...emptyState(), sort: state.sort });

/** Query a product link carries from a filtered listing (`?wattage=12w,9w&cct=6500k`), for variant preselection. */
export function productLinkQuery(state: ListingState): string {
  const parts = VARIANT_KEYS.filter((key) => state.options[key].length > 0).map(
    (key) => `${key}=${state.options[key].map(encodeURIComponent).join(',')}`,
  );
  return parts.length > 0 ? `?${parts.join('&')}` : '';
}
