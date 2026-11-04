import type { ProductCardModel } from '@/components/ProductCard';
import type { ProductDetailDto, ProductQuery } from './catalog';
import { messages } from './messages';
import type { HomeBlock } from './site-config';

type ProductRowData = Extract<HomeBlock, { type: 'productRow' }>['data'];

/** The catalog query behind a product row, or null for `handpicked` (those are read one by one). */
export function rowQuery(data: ProductRowData): ProductQuery | null {
  switch (data.source) {
    case 'newest':
      return { inStock: true, pageSize: data.limit };
    case 'discounted':
      return { inStock: true, discounted: true, pageSize: data.limit };
    case 'category':
      return data.categorySlug ? { category: data.categorySlug, pageSize: data.limit } : null;
    case 'handpicked':
      return null;
  }
}

/** A row without a title still gets a heading (headings are never empty). */
export function rowTitle(data: ProductRowData): string {
  if (data.title) return data.title;
  switch (data.source) {
    case 'newest':
      return messages.newestProducts;
    case 'discounted':
      return messages.discountedProducts;
    case 'handpicked':
      return messages.featuredProducts;
    default:
      return messages.productsHeading;
  }
}

/** A product card from a full product record (hand-picked rows): the lowest sell price among the available variants. */
export function cardFromDetail(product: ProductDetailDto): ProductCardModel | null {
  if (product.status !== 'published' || product.variants.length === 0) return null;
  const available = product.variants.filter((v) => v.inStock);
  const pool = available.length > 0 ? available : product.variants;
  const best = pool.reduce((a, b) => (b.price < a.price ? b : a));
  const image = product.images[0];
  return {
    slug: product.slug,
    name: product.name,
    path: product.path,
    brand: product.brand?.name ?? null,
    price: best.price,
    compareAtPrice: best.compareAtPrice,
    inStock: available.length > 0,
    imageUrl: image?.url ?? null,
    imageAlt: image?.alt ?? product.name,
  };
}
