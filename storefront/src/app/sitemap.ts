import type { MetadataRoute } from 'next';
import { getBlogSitemap } from '@/lib/blog';
import { getSitemap, parseUtc } from '@/lib/catalog';
import { getContentPages, isInfoSlug } from '@/lib/content';
import { absoluteUrl } from '@/lib/seo';

/** From the API: active categories and published products (never Draft or Archived), with their last change. */
export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const [{ categories, products }, pages, posts] = await Promise.all([getSitemap(), getContentPages(), getBlogSitemap()]);
  const entry = (path: string, updatedAtUtc: string | null) => {
    const lastModified = parseUtc(updatedAtUtc);
    return { url: absoluteUrl(path), ...(lastModified ? { lastModified } : {}) };
  };
  return [
    { url: absoluteUrl('/') },
    ...categories.map((c) => entry(`/category/${c.slug}`, c.updatedAtUtc)),
    ...products.map((p) => entry(`/product/${p.slug}`, p.updatedAtUtc)),
    ...pages.filter((p) => isInfoSlug(p.slug)).map((p) => entry(`/${p.slug}`, p.updatedAtUtc)),
    { url: absoluteUrl('/blog') },
    ...posts.map((p) => entry(`/blog/${p.slug}`, p.updatedAtUtc)),
  ];
}
