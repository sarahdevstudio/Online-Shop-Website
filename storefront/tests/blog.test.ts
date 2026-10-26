import { beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('next/server', () => ({ connection: vi.fn(async () => undefined) }));

const apiGet = vi.fn();
vi.mock('../src/lib/api', () => ({ apiGet, revalidate: { products: 60, categories: 300, sitemap: 300 } }));

const { getBlogList, getBlogPost, getBlogSitemap, isBlogSlug } = await import('../src/lib/blog');
const { pageMetadata } = await import('../src/lib/seo');
const { parsePageParam, pageOneRedirect, isPageInRange } = await import('../src/lib/paging');

describe('blog slugs', () => {
  it('accept the API slug format only', () => {
    for (const ok of ['led-guide', 'a', 'post-2', 't0123456789ab']) expect(isBlogSlug(ok), ok).toBe(true);
    for (const bad of ['', 'Led-Guide', 'led_guide', 'led--guide', '-led', 'led-', '../about', 'a/b', 'راهنما', 'a'.repeat(101)]) {
      expect(isBlogSlug(bad), bad).toBe(false);
    }
  });
});

describe('blog data', () => {
  beforeEach(() => {
    apiGet.mockReset();
  });

  it('requests the page asked for and caches for a minute', async () => {
    apiGet.mockResolvedValue({ items: [], page: 2, pageSize: 12, total: 13, totalPages: 2 });

    const list = await getBlogList(2);

    expect(apiGet).toHaveBeenCalledWith('/catalog/blog?page=2', 60);
    expect(list.totalPages).toBe(2);
  });

  it('turns an API 404 for a post into null (the page answers 404)', async () => {
    apiGet.mockResolvedValue(null);

    expect(await getBlogPost('missing-post')).toBeNull();
    expect(apiGet).toHaveBeenCalledWith('/catalog/blog/missing-post', 60);
  });

  it('has an empty sitemap feed when the API has none', async () => {
    apiGet.mockResolvedValue(null);

    expect(await getBlogSitemap()).toEqual([]);
  });
});

describe('blog paging and metadata', () => {
  it('treats a bad or out-of-range page as not found, and ?page=1 as a redirect to the clean URL', () => {
    expect(parsePageParam(undefined)).toBe(1);
    expect(parsePageParam('0')).toBeNull();
    expect(parsePageParam('abc')).toBeNull();
    expect(pageOneRedirect('/blog', { page: '1' })).toBe('/blog');
    expect(pageOneRedirect('/blog', { page: '2' })).toBeNull();
    expect(isPageInRange(1, 0)).toBe(true); // an empty blog still has page 1
    expect(isPageInRange(3, 2)).toBe(false);
  });

  it('is an indexable article with a self-canonical URL', () => {
    const meta = pageMetadata({ title: 'راهنما', description: 'خلاصه', path: '/blog/led-guide', type: 'article' });

    expect(meta.robots).toBeUndefined();
    expect(String(meta.alternates?.canonical)).toMatch(/\/blog\/led-guide$/);
    expect((meta.openGraph as { type?: string }).type).toBe('article');
  });
});
