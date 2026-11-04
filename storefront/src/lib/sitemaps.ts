import { getBlogSitemap } from './blog';
import { getSitemap, parseUtc } from './catalog';
import { getContentPages, isInfoSlug } from './content';
import { getNoIndex } from './entity-seo';
import { absoluteUrl } from './seo';

/**
 * The sitemap index (`/sitemap.xml`) and the four sitemaps behind it (`/sitemaps/products.xml`, `categories.xml`, `pages.xml`,
 * `blog.xml`), each with `<lastmod>` from the entity's last change. Only public, indexable URLs: published products, active
 * categories, info pages and published posts that did not ask for noindex (docs/17).
 */
export const SITEMAP_NAMES = ['products', 'categories', 'pages', 'blog'] as const;
export type SitemapName = (typeof SITEMAP_NAMES)[number];

export interface SitemapEntry {
  loc: string;
  lastmod: string | null;
}

export const escapeXml = (value: string) =>
  value.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&apos;');

const lastmodOf = (utc: string | null | undefined): string | null => {
  const date = parseUtc(utc ?? null);
  return date ? date.toISOString() : null;
};

export async function entriesFor(name: SitemapName): Promise<SitemapEntry[]> {
  const noindex = await getNoIndex();
  const entry = (path: string, updatedAtUtc: string | null): SitemapEntry => ({ loc: absoluteUrl(path), lastmod: lastmodOf(updatedAtUtc) });
  switch (name) {
    case 'products': {
      const { products } = await getSitemap();
      return products.filter((p) => !noindex.product.has(p.slug)).map((p) => entry(p.path, p.updatedAtUtc));
    }
    case 'categories': {
      const { categories } = await getSitemap();
      return categories.filter((c) => !noindex.category.has(c.slug)).map((c) => entry(c.path, c.updatedAtUtc));
    }
    case 'pages': {
      const pages = await getContentPages();
      return [entry('/', null), entry('/blog', null), ...pages.filter((p) => isInfoSlug(p.slug) && !noindex.contentPage.has(p.slug)).map((p) => entry(p.path, p.updatedAtUtc))];
    }
    case 'blog': {
      const posts = await getBlogSitemap();
      return posts.filter((p) => !noindex.blogPost.has(p.slug)).map((p) => entry(p.path, p.updatedAtUtc));
    }
  }
}

export function urlsetXml(entries: readonly SitemapEntry[]): string {
  const urls = entries.map((e) => `<url><loc>${escapeXml(e.loc)}</loc>${e.lastmod ? `<lastmod>${e.lastmod}</lastmod>` : ''}</url>`).join('');
  return `<?xml version="1.0" encoding="UTF-8"?><urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">${urls}</urlset>`;
}

export function indexXml(parts: readonly { name: SitemapName; entries: readonly SitemapEntry[] }[]): string {
  const items = parts
    .filter((part) => part.entries.length > 0)
    .map((part) => {
      const last = part.entries.map((e) => e.lastmod).filter((m): m is string => Boolean(m)).sort().at(-1);
      return `<sitemap><loc>${escapeXml(absoluteUrl(`/sitemaps/${part.name}.xml`))}</loc>${last ? `<lastmod>${last}</lastmod>` : ''}</sitemap>`;
    })
    .join('');
  return `<?xml version="1.0" encoding="UTF-8"?><sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">${items}</sitemapindex>`;
}

export const XML_HEADERS = { 'content-type': 'application/xml; charset=utf-8', 'cache-control': 'public, max-age=300, s-maxage=300' };
