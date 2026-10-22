import { cache } from 'react';
import type { ProductCardModel } from '@/components/ProductCard';
import { apiGet, revalidate } from './api';
import { OPTION_KEYS, type ListingState } from './listing';

/**
 * Catalog reads for the pages: API DTOs (`/api/v1/catalog/*`, see docs/06-catalog-design.md) mapped to the shapes
 * the components use. Every function is request-time and cached per `revalidate` in ./api.
 */

// ---- API DTOs ----

interface CategoryLinkDto {
  slug: string;
  name: string;
}

interface CategoryTreeDto extends CategoryLinkDto {
  children: CategoryLinkDto[];
}

interface CategoryDetailDto extends CategoryLinkDto {
  description: string | null;
  parent: CategoryLinkDto | null;
  children: CategoryLinkDto[];
}

interface ImageDto {
  url: string;
  alt: string;
}

export interface ProductCardDto {
  slug: string;
  name: string;
  brand: string | null;
  price: number;
  compareAtPrice: number | null;
  inStock: boolean;
  image: ImageDto | null;
}

export interface FacetOptionDto {
  slug: string;
  value: string;
  count: number;
}

export interface FacetsDto {
  brands: FacetOptionDto[];
  attributes: { code: string; name: string; unit: string | null; options: FacetOptionDto[] }[];
  price: { min: number; max: number } | null;
}

interface ProductListDto {
  items: ProductCardDto[];
  page: number;
  pageSize: number;
  total: number;
  facets?: FacetsDto;
}

export interface VariantDto {
  id: number;
  sku: string;
  price: number;
  compareAtPrice: number | null;
  inStock: boolean;
  /** attribute code → option slug (axes) or text. */
  attributes: Record<string, string>;
}

export interface VariantAxisDto {
  code: string;
  name: string;
  unit: string | null;
  options: { slug: string; value: string }[];
}

export interface ProductDetailDto {
  slug: string;
  name: string;
  description: string | null;
  status: 'published' | 'archived';
  brand: { slug: string; name: string } | null;
  breadcrumb: CategoryLinkDto[];
  images: ImageDto[];
  axes: VariantAxisDto[];
  variants: VariantDto[];
  specs: { code: string; name: string; value: string; unit: string | null }[];
  updatedAtUtc: string;
}

interface SitemapDto {
  categories: { slug: string; updatedAtUtc: string | null }[];
  products: { slug: string; updatedAtUtc: string | null }[];
}

// ---- page models ----

export interface Category {
  slug: string;
  name: string;
  description: string;
  parent: CategoryLinkDto | null;
  children: CategoryLinkDto[];
}

export interface ProductPage {
  items: ProductCardModel[];
  total: number;
  page: number;
  pageCount: number;
  /** Counts per option; a facet ignores its own selection (OR within, AND across). */
  facets: FacetsDto;
}

export const PAGE_SIZE = 24;

export function toProductCard(dto: ProductCardDto): ProductCardModel {
  return {
    slug: dto.slug,
    name: dto.name,
    brand: dto.brand,
    price: dto.price,
    compareAtPrice: dto.compareAtPrice,
    inStock: dto.inStock,
    imageUrl: dto.image?.url ?? null,
    imageAlt: dto.image?.alt ?? dto.name,
  };
}

export function toProductPage(dto: ProductListDto): ProductPage {
  return {
    items: dto.items.map(toProductCard),
    total: dto.total,
    page: dto.page,
    pageCount: Math.max(1, Math.ceil(dto.total / dto.pageSize)),
    facets: dto.facets ?? { brands: [], attributes: [], price: null },
  };
}

// ---- reads (React cache() dedupes repeated calls within one render, e.g. layout + page) ----

export const getCategoryTree = cache(async (): Promise<CategoryTreeDto[]> =>
  (await apiGet<CategoryTreeDto[]>('/catalog/categories', revalidate.categories)) ?? []);

export const getCategory = cache(async (slug: string): Promise<Category | null> => {
  const dto = await apiGet<CategoryDetailDto>(`/catalog/categories/${encodeURIComponent(slug)}`, revalidate.categories);
  return dto && { slug: dto.slug, name: dto.name, description: dto.description ?? '', parent: dto.parent, children: dto.children };
});

export interface ProductQuery {
  category?: string;
  q?: string;
  page?: number;
  pageSize?: number;
  inStock?: boolean;
  /** Category-page filters and sort (see ./listing). */
  listing?: ListingState;
}

/** The API's q limit; longer input is cut rather than turned into an error page. */
export const Q_MAX_LENGTH = 100;

export function productListPath({ category, q, page = 1, pageSize = PAGE_SIZE, inStock, listing }: ProductQuery): string {
  const params = new URLSearchParams();
  if (category) params.set('category', category);
  const query = q?.trim().slice(0, Q_MAX_LENGTH);
  if (query) params.set('q', query);
  for (const key of OPTION_KEYS) {
    const values = listing?.options[key] ?? [];
    if (values.length > 0) params.set(key, values.join(','));
  }
  if (listing?.minPrice != null) params.set('minPrice', String(listing.minPrice));
  if (listing?.maxPrice != null) params.set('maxPrice', String(listing.maxPrice));
  if (inStock || listing?.inStock) params.set('inStock', 'true');
  if (listing?.sort) params.set('sort', listing.sort);
  const pageNumber = listing?.page ?? page;
  if (pageNumber > 1) params.set('page', String(pageNumber));
  params.set('pageSize', String(pageSize));
  return `/catalog/products?${params}`;
}

const getProductPage = cache(async (path: string): Promise<ProductPage | null> => {
  const dto = await apiGet<ProductListDto>(path, revalidate.products);
  return dto && toProductPage(dto);
});

/** Null when the API says the category doesn't exist. Same query = one API call per render (metadata + page). */
export function getProducts(query: ProductQuery): Promise<ProductPage | null> {
  return getProductPage(productListPath(query));
}

export const getProduct = cache(async (slug: string): Promise<ProductDetailDto | null> =>
  apiGet<ProductDetailDto>(`/catalog/products/${encodeURIComponent(slug)}`, revalidate.products));

/**
 * The API's timestamps are UTC but may come without a zone designator (SQL Server datetime2); `new Date()` would
 * read those as local time, so treat a missing designator as UTC.
 */
export function parseUtc(value: string | null): Date | null {
  if (!value) return null;
  const date = new Date(/(Z|[+-]\d{2}:?\d{2})$/i.test(value) ? value : `${value}Z`);
  return Number.isNaN(date.getTime()) ? null : date;
}

export async function getSitemap(): Promise<SitemapDto> {
  return (await apiGet<SitemapDto>('/catalog/sitemap', revalidate.sitemap)) ?? { categories: [], products: [] };
}
