import type { MetadataRoute } from 'next';
import { getSitemap, parseUtc } from '@/lib/catalog';
import { absoluteUrl } from '@/lib/seo';

/** From the API: active categories and published products (never Draft or Archived), with their last change. */
export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const { categories, products } = await getSitemap();
  const entry = (path: string, updatedAtUtc: string | null) => {
    const lastModified = parseUtc(updatedAtUtc);
    return { url: absoluteUrl(path), ...(lastModified ? { lastModified } : {}) };
  };
  return [
    { url: absoluteUrl('/') },
    ...categories.map((c) => entry(`/category/${c.slug}`, c.updatedAtUtc)),
    ...products.map((p) => entry(`/product/${p.slug}`, p.updatedAtUtc)),
  ];
}
