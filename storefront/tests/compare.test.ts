import { describe, expect, it } from 'vitest';
import type { ProductDetailDto } from '../src/lib/catalog';
import { buildComparison, COMPARE_MAX, parseCompare, serializeCompare, toggleCompare } from '../src/lib/compare';

const product = (over: Partial<ProductDetailDto> & { slug: string }): ProductDetailDto => ({
  name: over.name ?? over.slug,
  path: `/product/${over.slug}`,
  description: null,
  status: 'published',
  brand: null,
  breadcrumb: [],
  images: [],
  axes: [],
  variants: [{ id: 1, sku: `${over.slug}-1`, price: 100_000, compareAtPrice: null, inStock: true, attributes: {} }],
  specs: [],
  updatedAtUtc: '2026-11-01T00:00:00',
  ...over,
});

describe('the comparison list (a cookie of slugs)', () => {
  it('keeps valid, unique slugs, at most four, and ignores the rest', () => {
    expect(parseCompare(undefined)).toEqual([]);
    expect(parseCompare('')).toEqual([]);
    expect(parseCompare('a,b,a,c')).toEqual(['a', 'b', 'c']);
    expect(parseCompare('a,b,c,d,e,f')).toEqual(['a', 'b', 'c', 'd']);
    expect(parseCompare('led-9w,<script>,../x,UPPER, ,ok1')).toEqual(['led-9w', 'ok1']);
    expect(serializeCompare(['a', 'b'])).toBe('a,b');
    expect(COMPARE_MAX).toBe(4);
  });

  it('toggles: adds, removes, and refuses a fifth', () => {
    expect(toggleCompare([], 'a')).toEqual({ slugs: ['a'], changed: 'added' });
    expect(toggleCompare(['a', 'b'], 'a')).toEqual({ slugs: ['b'], changed: 'removed' });
    expect(toggleCompare(['a', 'b', 'c', 'd'], 'e')).toEqual({ slugs: ['a', 'b', 'c', 'd'], changed: 'full' });
    expect(toggleCompare(['a', 'b', 'c', 'd'], 'd').changed).toBe('removed'); // a full list can still drop one
  });
});

describe('the comparison table', () => {
  const a = product({
    slug: 'a', name: 'لامپ الف', brand: { slug: 'x', name: 'برند ایکس' },
    axes: [{ code: 'wattage', name: 'توان', unit: null, options: [{ slug: '9w', value: '۹ وات' }, { slug: '12w', value: '۱۲ وات' }] }],
    specs: [{ code: 'base', name: 'سرپیچ', value: 'E27', unit: null }, { code: 'lumen', name: 'لومن', value: '۱۰۵۰', unit: null }],
    variants: [
      { id: 1, sku: 'a1', price: 98_000, compareAtPrice: null, inStock: true, attributes: {} },
      { id: 2, sku: 'a2', price: 120_000, compareAtPrice: null, inStock: false, attributes: {} },
    ],
  });
  const b = product({
    slug: 'b', name: 'لامپ ب', brand: null,
    specs: [{ code: 'base', name: 'سرپیچ', value: 'E27', unit: null }, { code: 'life', name: 'عمر', value: '۱۵۰۰۰', unit: 'ساعت' }],
    variants: [{ id: 3, sku: 'b1', price: 98_000, compareAtPrice: null, inStock: false, attributes: {} }],
  });

  it('has price, brand and stock first, then each attribute once, in order of first appearance', () => {
    const { columns, rows } = buildComparison([a, b]);
    expect(columns.map((c) => c.slug)).toEqual(['a', 'b']);
    expect(rows.map((r) => r.label)).toEqual(['قیمت', 'برند', 'موجودی', 'توان', 'سرپیچ', 'لومن', 'عمر']);
  });

  it('shows ranges, option lists, units and a dash for a missing value', () => {
    const rows = Object.fromEntries(buildComparison([a, b]).rows.map((r) => [r.label, r.values]));
    expect(rows['قیمت'][0]).toContain('–'); // a range for two variants
    expect(rows['قیمت'][1]).not.toContain('–');
    expect(rows['توان'][0]).toContain('۹ وات');
    expect(rows['توان'][0]).toContain('۱۲ وات');
    expect(rows['توان'][1]).toBe('—');
    expect(rows['عمر'][1]).toBe('۱۵۰۰۰ ساعت');
    expect(rows['برند']).toEqual(['برند ایکس', '—']);
    expect(rows['موجودی']).toEqual(['موجود', 'ناموجود']); // in stock when any variant is
  });

  it('marks exactly the rows where the products differ', () => {
    const differs = Object.fromEntries(buildComparison([a, b]).rows.map((r) => [r.label, r.differs]));
    expect(differs).toEqual({ 'قیمت': true, 'برند': true, 'موجودی': true, 'توان': true, 'سرپیچ': false, 'لومن': true, 'عمر': true });
    expect(buildComparison([a]).rows.every((r) => !r.differs)).toBe(true); // nothing to differ from
    expect(buildComparison([b, { ...b, slug: 'c', name: 'ج' }]).rows.filter((r) => r.differs)).toEqual([]);
  });
});
