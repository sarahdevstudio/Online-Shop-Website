import { apiGet } from './api';

/**
 * What an admin entered for one page's search presence (docs/17): title / description / share image overrides, noindex, the FAQ
 * (products, categories) and the category buying guide. Empty fields mean "use the generated default". Any API trouble means
 * "no overrides": SEO extras must never take a page down.
 */
export interface EntitySeo {
  title: string | null;
  description: string | null;
  ogImageUrl: string | null;
  noIndex: boolean;
  faq: { question: string; answer: string }[];
  intro: string | null;
}

export const EMPTY_SEO: EntitySeo = { title: null, description: null, ogImageUrl: null, noIndex: false, faq: [], intro: null };

export type SeoType = 'product' | 'category' | 'contentPage' | 'blogPost';

const SECONDS = 60;

export async function getEntitySeo(type: SeoType, slug: string): Promise<EntitySeo> {
  try {
    const dto = await apiGet<Partial<EntitySeo> & { faq?: EntitySeo['faq'] }>(`/catalog/seo/${type}/${encodeURIComponent(slug)}`, SECONDS);
    return dto ? { ...EMPTY_SEO, ...dto, faq: dto.faq ?? [] } : EMPTY_SEO;
  } catch {
    return EMPTY_SEO;
  }
}

/** Slugs asked not to be indexed, by type; the sitemaps leave them out. */
export async function getNoIndex(): Promise<Record<SeoType, Set<string>>> {
  const empty = { product: new Set<string>(), category: new Set<string>(), contentPage: new Set<string>(), blogPost: new Set<string>() };
  try {
    const dto = await apiGet<Record<SeoType, string[]>>('/catalog/seo/noindex', SECONDS);
    if (!dto) return empty;
    return { product: new Set(dto.product ?? []), category: new Set(dto.category ?? []), contentPage: new Set(dto.contentPage ?? []), blogPost: new Set(dto.blogPost ?? []) };
  } catch {
    return empty;
  }
}
