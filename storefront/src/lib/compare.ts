import { formatNumber } from './format';
import { compareMessages as m } from './compare-messages';
import { messages } from './messages';
import type { ProductDetailDto } from './catalog';

/**
 * Product comparison: up to four products, remembered in a cookie (no login). The cookie holds product slugs only; the page
 * reads each product again, so prices and stock are always current and an unpublished product simply drops out.
 */
export const COMPARE_COOKIE = 'compare';
export const COMPARE_MAX = 4;
export const COMPARE_MAX_AGE_SECONDS = 30 * 24 * 60 * 60;

const SLUG = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

/** The slugs in a cookie value: valid, unique, at most four; anything else is ignored. */
export function parseCompare(value: string | undefined | null): string[] {
  if (!value) return [];
  const seen = new Set<string>();
  for (const part of value.split(',')) {
    const slug = part.trim();
    if (slug.length > 0 && slug.length <= 100 && SLUG.test(slug)) seen.add(slug);
    if (seen.size === COMPARE_MAX) break;
  }
  return [...seen];
}

export const serializeCompare = (slugs: readonly string[]) => slugs.join(',');

export type ToggleResult = { slugs: string[]; changed: 'added' | 'removed' | 'full' };

/** Adds the slug, or removes it when it is already there; refuses a fifth product. */
export function toggleCompare(current: readonly string[], slug: string): ToggleResult {
  if (current.includes(slug)) return { slugs: current.filter((s) => s !== slug), changed: 'removed' };
  if (current.length >= COMPARE_MAX) return { slugs: [...current], changed: 'full' };
  return { slugs: [...current, slug], changed: 'added' };
}

export interface CompareColumn {
  slug: string;
  name: string;
  path: string;
  image: { url: string; alt: string } | null;
}

export interface CompareRow {
  label: string;
  /** One cell per column, in column order; the "none" dash where a product has no such attribute. */
  values: string[];
  differs: boolean;
}

export interface Comparison {
  columns: CompareColumn[];
  rows: CompareRow[];
}

const NONE = () => m.none;

function priceText(product: ProductDetailDto): string {
  const prices = product.variants.map((v) => v.price);
  if (prices.length === 0) return NONE();
  const min = Math.min(...prices);
  const max = Math.max(...prices);
  return min === max ? formatNumber(min) : `${formatNumber(min)} – ${formatNumber(max)}`;
}

/**
 * The comparison table from full product records: price, brand and stock first, then the attributes (the variant axes such as
 * wattage list all their options; text specs such as lumen show their value), each attribute once, in the order they first appear.
 * A row `differs` when the products do not all say the same thing (a missing value counts as different from a present one).
 */
export function buildComparison(products: readonly ProductDetailDto[]): Comparison {
  const columns = products.map((p) => ({ slug: p.slug, name: p.name, path: p.path, image: p.images[0] ? { url: p.images[0].url, alt: p.images[0].alt } : null }));
  const rows: CompareRow[] = [];
  const add = (label: string, values: string[]) => rows.push({ label, values, differs: new Set(values.map((v) => v.trim())).size > 1 });

  add(m.rowPrice, products.map(priceText));
  add(m.rowBrand, products.map((p) => p.brand?.name ?? NONE()));
  add(m.rowStock, products.map((p) => (p.variants.some((v) => v.inStock) ? m.inStock : m.outOfStock)));

  // attribute label → one value per product
  const labels: string[] = [];
  const byProduct = products.map((p) => {
    const values = new Map<string, string>();
    for (const axis of p.axes) values.set(axis.name, axis.options.map((o) => (axis.unit ? `${o.value} ${axis.unit}` : o.value)).join(messages.listSeparator));
    for (const spec of p.specs) values.set(spec.name, spec.unit ? `${spec.value} ${spec.unit}` : spec.value);
    for (const label of values.keys()) if (!labels.includes(label)) labels.push(label);
    return values;
  });
  for (const label of labels) add(label, byProduct.map((values) => values.get(label) ?? NONE()));

  return { columns, rows };
}
