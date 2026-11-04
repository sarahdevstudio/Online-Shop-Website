import { describe, expect, it } from 'vitest';
import type { ProductDetailDto } from '../src/lib/catalog';
import { productListPath } from '../src/lib/catalog';
import { cardFromDetail, rowQuery, rowTitle } from '../src/lib/home-products';

const row = (source: 'newest' | 'discounted' | 'category' | 'handpicked', extra = {}) => ({ title: '', source, limit: 8, categorySlug: null, productSlugs: [], viewAllHref: null, ...extra });

describe('rowQuery', () => {
  it('maps every source to a catalog query', () => {
    expect(rowQuery(row('newest'))).toEqual({ inStock: true, pageSize: 8 });
    expect(rowQuery(row('discounted'))).toEqual({ inStock: true, discounted: true, pageSize: 8 });
    expect(rowQuery(row('category', { categorySlug: 'lamps', limit: 4 }))).toEqual({ category: 'lamps', pageSize: 4 });
    expect(rowQuery(row('category'))).toBeNull();
    expect(rowQuery(row('handpicked', { productSlugs: ['a'] }))).toBeNull();
  });

  it('sends the discounted filter to the API', () => {
    expect(productListPath(rowQuery(row('discounted'))!)).toContain('discounted=true');
    expect(productListPath({ pageSize: 8 })).not.toContain('discounted');
  });

  it('never leaves a row without a heading', () => {
    expect(rowTitle(row('newest'))).toBe('جدیدترین محصولات');
    expect(rowTitle(row('discounted'))).toBe('محصولات تخفیف‌دار');
    expect(rowTitle(row('newest', { title: 'ویژه' }))).toBe('ویژه');
  });
});

const detail = (over: Partial<ProductDetailDto> = {}): ProductDetailDto => ({
  slug: 'p', name: 'محصول', path: '/product/p', description: null, status: 'published', brand: { slug: 'b', name: 'برند' }, breadcrumb: [], images: [{ url: '/media/x.jpg', alt: 'x' }],
  axes: [], variants: [
    { id: 1, sku: 'a', price: 200, compareAtPrice: 250, inStock: true, attributes: {} },
    { id: 2, sku: 'b', price: 100, compareAtPrice: null, inStock: false, attributes: {} },
    { id: 3, sku: 'c', price: 150, compareAtPrice: null, inStock: true, attributes: {} },
  ], specs: [], updatedAtUtc: '', ...over,
});

describe('cardFromDetail', () => {
  it('uses the lowest price among available variants', () => {
    expect(cardFromDetail(detail())).toMatchObject({ slug: 'p', price: 150, compareAtPrice: null, inStock: true, imageUrl: '/media/x.jpg', brand: 'برند' });
  });
  it('falls back to all variants when nothing is in stock', () => {
    const card = cardFromDetail(detail({ variants: [{ id: 2, sku: 'b', price: 100, compareAtPrice: null, inStock: false, attributes: {} }] }));
    expect(card).toMatchObject({ price: 100, inStock: false });
  });
  it('skips archived and variant-less products', () => {
    expect(cardFromDetail(detail({ status: 'archived' }))).toBeNull();
    expect(cardFromDetail(detail({ variants: [] }))).toBeNull();
  });
});
