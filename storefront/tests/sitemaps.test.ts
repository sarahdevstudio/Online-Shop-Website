import { beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('next/server', () => ({ connection: vi.fn(async () => undefined) }));
const apiGet = vi.fn();
vi.mock('../src/lib/api', () => ({ apiGet, revalidate: { products: 60, categories: 300, sitemap: 300 } }));

const { SITEMAP_NAMES, entriesFor, escapeXml, indexXml, urlsetXml } = await import('../src/lib/sitemaps');

function api(overrides: Record<string, unknown> = {}) {
  apiGet.mockImplementation(async (path: string) => {
    if (path in overrides) return overrides[path];
    if (path === '/catalog/sitemap') {
      return {
        categories: [{ slug: 'lamps', path: '/category/lamps', updatedAtUtc: '2026-10-20T08:00:00' }, { slug: 'old', path: '/category/old', updatedAtUtc: null }],
        products: [{ slug: 'led', path: '/product/led', updatedAtUtc: '2026-10-22T09:30:00' }, { slug: 'hidden', path: '/product/hidden', updatedAtUtc: '2026-10-23T09:30:00' }],
      };
    }
    if (path === '/catalog/pages') return [{ slug: 'about', path: '/about', title: 'x', updatedAtUtc: '2026-10-26T10:30:00' }, { slug: 'privacy', path: '/privacy', title: 'x', updatedAtUtc: '2026-10-25T10:30:00' }];
    if (path === '/catalog/blog/sitemap') return [{ slug: 'guide', path: '/blog/guide', updatedAtUtc: '2026-10-27T11:00:00' }];
    if (path === '/catalog/seo/noindex') return { product: [], category: [], contentPage: [], blogPost: [] };
    return null;
  });
}

describe('split sitemaps', () => {
  beforeEach(() => apiGet.mockReset());

  it('are four, one per kind of page', () => {
    expect([...SITEMAP_NAMES]).toEqual(['products', 'categories', 'pages', 'blog']);
  });

  it('carry absolute URLs and an ISO lastmod from the last change (the API sends UTC without a zone)', async () => {
    api();
    const products = await entriesFor('products');
    expect(products).toEqual([
      { loc: 'http://localhost:3000/product/led', lastmod: '2026-10-22T09:30:00.000Z' },
      { loc: 'http://localhost:3000/product/hidden', lastmod: '2026-10-23T09:30:00.000Z' },
    ]);
    expect((await entriesFor('categories')).find((e) => e.loc.endsWith('/old'))?.lastmod).toBeNull(); // no date is better than a made-up one
    expect((await entriesFor('pages')).map((e) => e.loc)).toEqual(['http://localhost:3000/', 'http://localhost:3000/blog', 'http://localhost:3000/about', 'http://localhost:3000/privacy']);
    expect((await entriesFor('blog'))[0].lastmod).toBe('2026-10-27T11:00:00.000Z');
  });

  it('leave out what asked for noindex, of every kind', async () => {
    api({ '/catalog/seo/noindex': { product: ['hidden'], category: ['old'], contentPage: ['privacy'], blogPost: ['guide'] } });
    expect((await entriesFor('products')).map((e) => e.loc)).toEqual(['http://localhost:3000/product/led']);
    expect((await entriesFor('categories')).map((e) => e.loc)).toEqual(['http://localhost:3000/category/lamps']);
    expect((await entriesFor('pages')).some((e) => e.loc.endsWith('/privacy'))).toBe(false);
    expect(await entriesFor('blog')).toEqual([]);
  });

  it('keep working (no exclusions) when the noindex list cannot be read', async () => {
    api({ '/catalog/seo/noindex': null });
    expect((await entriesFor('products')).length).toBe(2);
  });

  it('write valid, escaped XML', async () => {
    const xml = urlsetXml([{ loc: 'http://x/a?b=1&c="2"<3>', lastmod: '2026-10-22T09:30:00.000Z' }, { loc: 'http://x/b', lastmod: null }]);
    expect(xml).toContain('<loc>http://x/a?b=1&amp;c=&quot;2&quot;&lt;3&gt;</loc><lastmod>2026-10-22T09:30:00.000Z</lastmod>');
    expect(xml).toContain('<url><loc>http://x/b</loc></url>');
    expect(xml.startsWith('<?xml version="1.0" encoding="UTF-8"?><urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">')).toBe(true);
    expect(escapeXml(`<a href="x">&'</a>`)).toBe('&lt;a href=&quot;x&quot;&gt;&amp;&apos;&lt;/a&gt;');
  });

  it('the index lists only non-empty sitemaps, each with the newest lastmod inside it', async () => {
    api();
    const parts = await Promise.all(SITEMAP_NAMES.map(async (name) => ({ name, entries: await entriesFor(name) })));
    parts[3] = { name: 'blog', entries: [] };
    const xml = indexXml(parts);
    expect(xml).toContain('<sitemap><loc>http://localhost:3000/sitemaps/products.xml</loc><lastmod>2026-10-23T09:30:00.000Z</lastmod></sitemap>');
    expect(xml).toContain('/sitemaps/categories.xml');
    expect(xml).toContain('/sitemaps/pages.xml');
    expect(xml).not.toContain('/sitemaps/blog.xml');
    expect(xml).toContain('<sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">');
  });
});
