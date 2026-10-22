import { describe, expect, it } from 'vitest';
import {
  clearFilters,
  emptyState,
  hasFiltersOrSort,
  listingHref,
  listingIndexing,
  parseListing,
  productLinkQuery,
  serializeListing,
  serializeRequest,
  toggleOption,
  withInStock,
  withoutPrice,
  withPage,
  withSort,
  type AllowedOptions,
} from '../src/lib/listing';
import type { SearchParams } from '../src/lib/paging';
import { pageMetadata } from '../src/lib/seo';

const allowed: AllowedOptions = {
  brand: new Set(['pars-shahab', 'zar']),
  wattage: new Set(['9w', '12w', '15w']),
  base: new Set(['e27', 'e14']),
  cct: new Set(['3000k', '6500k']),
};

const normalize = (params: SearchParams) => serializeListing(parseListing(params, allowed).state);
/** True when the page would 308-redirect this request. */
const redirects = (params: SearchParams) => serializeRequest(params) !== normalize(params);

describe('URL normalization', () => {
  it('orders keys as brand, wattage, base, cct, minPrice, maxPrice, inStock, sort, page', () => {
    expect(normalize({ sort: 'price-asc', cct: '6500k', page: '2', brand: 'zar', base: 'e27', inStock: '1', wattage: '9w', maxPrice: '200000', minPrice: '1000' })).toBe(
      'brand=zar&wattage=9w&base=e27&cct=6500k&minPrice=1000&maxPrice=200000&inStock=1&sort=price-asc&page=2',
    );
  });

  it('sorts and deduplicates values, merging comma lists and repeated keys', () => {
    expect(normalize({ wattage: ['9w', '12w,9w'] })).toBe('wattage=12w,9w');
    expect(normalize({ wattage: '9w,9w, 12W ' })).toBe('wattage=12w,9w');
  });

  it('drops unknown keys and values that do not exist in the category', () => {
    expect(normalize({ wattage: 'bogus', utm_source: 'x', q: 'lamp', color: 'red' })).toBe('');
    expect(normalize({ wattage: '9w,20w', brand: 'no-such-brand' })).toBe('wattage=9w');
  });

  it('matches keys case-insensitively and writes the canonical spelling', () => {
    expect(normalize({ Wattage: '9w', INSTOCK: 'true', minprice: '5000' })).toBe('wattage=9w&minPrice=5000&inStock=1');
  });

  it('drops defaults: page 1, newest, inStock off, empty prices', () => {
    expect(normalize({ page: '1', sort: 'newest', inStock: '0', minPrice: '', maxPrice: '' })).toBe('');
  });

  it('accepts Persian digits and separators in prices, drops invalid ones, and swaps an inverted range', () => {
    expect(normalize({ minPrice: '۱۰۰٬۰۰۰' })).toBe('minPrice=100000');
    expect(normalize({ minPrice: '-5', maxPrice: 'abc' })).toBe('');
    expect(normalize({ minPrice: '500000', maxPrice: '100000' })).toBe('minPrice=100000&maxPrice=500000');
  });

  it('keeps the existing page rules: invalid page is a 404, not a redirect', () => {
    expect(parseListing({ page: 'abc' }, allowed).invalidPage).toBe(true);
    expect(parseListing({ page: '0' }, allowed).invalidPage).toBe(true);
    expect(parseListing({ page: ['2', '3'] }, allowed).invalidPage).toBe(true);
    expect(parseListing({ page: '2' }, allowed)).toMatchObject({ invalidPage: false, state: { page: 2 } });
  });

  it('redirects every non-normalized form and nothing else', () => {
    expect(redirects({})).toBe(false);
    expect(redirects({ page: '2' })).toBe(false);
    expect(redirects({ wattage: '12w,9w', cct: '6500k' })).toBe(false);
    expect(redirects({ cct: '6500k', wattage: '12w,9w' })).toBe(true); // key order
    expect(redirects({ wattage: '9w,12w' })).toBe(true); // value order
    expect(redirects({ wattage: ['12w', '9w'] })).toBe(true); // repeated key
    expect(redirects({ wattage: 'bogus' })).toBe(true); // → clean URL
    expect(redirects({ page: '1' })).toBe(true);
    expect(redirects({ wattage: '9w', utm_source: 'x' })).toBe(true);
  });

  it('round-trips: a normalized URL parses back to itself', () => {
    const qs = 'brand=pars-shahab,zar&wattage=12w,9w&minPrice=1000&inStock=1&sort=price-desc&page=3';
    expect(normalize(Object.fromEntries(new URLSearchParams(qs)))).toBe(qs);
  });
});

describe('filter link building', () => {
  const base = '/category/led-bulbs';
  const state = parseListing({ wattage: '9w', cct: '6500k', sort: 'price-asc', page: '3' }, allowed).state;

  it('toggles a value on and off and always resets to page 1', () => {
    expect(listingHref(base, toggleOption(state, 'wattage', '12w'))).toBe(`${base}?wattage=12w,9w&cct=6500k&sort=price-asc`);
    expect(listingHref(base, toggleOption(state, 'wattage', '9w'))).toBe(`${base}?cct=6500k&sort=price-asc`);
  });

  it('builds sort, in-stock, price-removal and page links', () => {
    expect(listingHref(base, withSort(state, null))).toBe(`${base}?wattage=9w&cct=6500k`);
    expect(listingHref(base, withInStock(state, true))).toBe(`${base}?wattage=9w&cct=6500k&inStock=1&sort=price-asc`);
    const priced = parseListing({ minPrice: '1000', wattage: '9w' }, allowed).state;
    expect(listingHref(base, withoutPrice(priced))).toBe(`${base}?wattage=9w`);
    expect(listingHref(base, withPage(state, 2))).toBe(`${base}?wattage=9w&cct=6500k&sort=price-asc&page=2`);
    expect(listingHref(base, withPage(state, 1))).toBe(`${base}?wattage=9w&cct=6500k&sort=price-asc`);
  });

  it('"clear all" removes filters but keeps the sort', () => {
    expect(listingHref(base, clearFilters(state))).toBe(`${base}?sort=price-asc`);
    expect(listingHref(base, clearFilters(emptyState()))).toBe(base);
  });

  it('every link it builds is already normalized (no redirect when followed)', () => {
    const links = [toggleOption(state, 'wattage', '12w'), withSort(state, 'price-desc'), withPage(state, 5), clearFilters(state)];
    for (const next of links) {
      const qs = serializeListing(next);
      expect(normalize(Object.fromEntries(new URLSearchParams(qs)))).toBe(qs);
    }
  });

  it('product links carry only the active wattage/cct values', () => {
    const filtered = parseListing({ brand: 'zar', wattage: '9w,12w', base: 'e27', cct: '3000k', sort: 'price-asc' }, allowed).state;
    expect(productLinkQuery(filtered)).toBe('?wattage=12w,9w&cct=3000k');
    expect(productLinkQuery(parseListing({ brand: 'zar' }, allowed).state)).toBe('');
  });
});

describe('robots and canonical rules', () => {
  const meta = (params: SearchParams) => {
    const { state } = parseListing(params, allowed);
    return pageMetadata({ title: 't', description: 'd', path: listingHref('/category/led-bulbs', state), ...listingIndexing(state) });
  };

  it('clean category URL (also ?page=N) is indexable and self-canonical', () => {
    expect(meta({}).robots).toBeUndefined();
    expect(meta({}).alternates?.canonical).toBe('http://localhost:3000/category/led-bulbs');
    expect(meta({ page: '2' }).alternates?.canonical).toBe('http://localhost:3000/category/led-bulbs?page=2');
  });

  it.each([{ wattage: '9w' }, { sort: 'price-asc' }, { inStock: '1' }, { minPrice: '1000' }, { brand: 'zar', page: '2' }])(
    'any filter or sort (%j) is noindex, follow with no canonical',
    (params) => {
      const m = meta(params);
      expect(m.robots).toEqual({ index: false, follow: true });
      expect(m.alternates).toBeUndefined();
    },
  );

  it('noindex pages elsewhere stay noindex, nofollow', () => {
    expect(pageMetadata({ title: 't', description: 'd', path: '/search', noindex: true }).robots).toEqual({ index: false, follow: false });
  });

  it('hasFiltersOrSort treats sort as a non-indexable param', () => {
    expect(hasFiltersOrSort(parseListing({ sort: 'price-desc' }, allowed).state)).toBe(true);
    expect(hasFiltersOrSort(parseListing({ page: '4' }, allowed).state)).toBe(false);
  });
});
