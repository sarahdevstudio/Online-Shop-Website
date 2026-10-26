import { beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('next/server', () => ({ connection: vi.fn(async () => undefined) }));

const apiGet = vi.fn();
vi.mock('../src/lib/api', () => ({ apiGet, revalidate: { products: 60, categories: 300, sitemap: 300 } }));

const { DEFAULT_TITLES, INFO_SLUGS, getInfoLinks, isInfoSlug } = await import('../src/lib/content');
const { contactJsonLd, pageMetadata } = await import('../src/lib/seo');
const { default: sitemap } = await import('../src/app/sitemap');

describe('info page slugs', () => {
  it('are exactly the five fixed pages', () => {
    expect([...INFO_SLUGS]).toEqual(['about', 'contact', 'terms', 'returns', 'privacy']);
    expect(isInfoSlug('about')).toBe(true);
    expect(isInfoSlug('cart')).toBe(false);
    expect(isInfoSlug('../about')).toBe(false);
    expect(isInfoSlug('')).toBe(false);
  });
});

describe('footer links', () => {
  beforeEach(() => {
    apiGet.mockReset(); // no return value: vitest would run a returned function as the teardown hook
  });

  it('use the titles from the API and link to /slug', async () => {
    apiGet.mockResolvedValue(INFO_SLUGS.map((slug) => ({ slug, title: `عنوان ${slug}`, updatedAtUtc: '2026-10-26T10:00:00' })));

    const links = await getInfoLinks();

    expect(links.map((l) => l.href)).toEqual(['/about', '/contact', '/terms', '/returns', '/privacy']);
    expect(links[0].label).toBe('عنوان about');
  });

  it('fall back to default titles when the API is down: the footer never breaks a page', async () => {
    apiGet.mockImplementation(() => {
      throw new Error('API down');
    });

    const links = await getInfoLinks();

    expect(links).toHaveLength(5);
    expect(links[1]).toEqual({ label: DEFAULT_TITLES.contact, href: '/contact' });
  });
});

describe('sitemap', () => {
  beforeEach(() => {
    apiGet.mockReset(); // no return value: vitest would run a returned function as the teardown hook
  });

  it('lists the info pages with their last modification', async () => {
    apiGet.mockImplementation(async (path: string) => {
      if (path === '/catalog/sitemap') return { categories: [{ slug: 'lamps', updatedAtUtc: '2026-10-20T08:00:00' }], products: [] };
      if (path === '/catalog/pages') return INFO_SLUGS.map((slug) => ({ slug, title: slug, updatedAtUtc: '2026-10-26T10:30:00' }));
      return null;
    });

    const entries = await sitemap();

    const pages = entries.filter((e) => /\/(about|contact|terms|returns|privacy)$/.test(e.url));
    expect(pages).toHaveLength(5);
    for (const page of pages) {
      expect(page.lastModified).toBeInstanceOf(Date);
      expect((page.lastModified as Date).toISOString()).toBe('2026-10-26T10:30:00.000Z'); // the API sends UTC without a zone suffix
    }
    expect(entries.some((e) => e.url.endsWith('/category/lamps'))).toBe(true);
  });

  it('ignores a page slug the storefront has no route for', async () => {
    apiGet.mockImplementation(async (path: string) =>
      path === '/catalog/pages' ? [{ slug: 'evil', title: 'x', updatedAtUtc: '2026-10-26T10:30:00' }] : { categories: [], products: [] },
    );

    expect((await sitemap()).some((e) => e.url.endsWith('/evil'))).toBe(false);
  });
});

describe('info page metadata', () => {
  it('is indexable and self-canonical', () => {
    const meta = pageMetadata({ title: 'درباره', description: 'متن', path: '/about' });

    expect(meta.robots).toBeUndefined(); // no noindex
    expect(String(meta.alternates?.canonical)).toMatch(/\/about$/);
  });
});

describe('contact structured data', () => {
  it('is a LocalBusiness only when an address is set', () => {
    const withAddress = contactJsonLd({ phone: '02112345678', address: 'تهران، خیابان آزادی', hours: '۹ تا ۱۸', instagram: 'myshop' });
    expect(withAddress['@type']).toBe('LocalBusiness');
    expect(withAddress).toMatchObject({
      telephone: '02112345678',
      address: { '@type': 'PostalAddress', streetAddress: 'تهران، خیابان آزادی', addressCountry: 'IR' },
      sameAs: ['https://www.instagram.com/myshop/'],
    });
  });

  it('is a plain Organization (with a contact point) without an address', () => {
    const data = contactJsonLd({ phone: '02112345678', address: '  ', hours: '', instagram: '' });
    expect(data['@type']).toBe('Organization');
    expect(data).toMatchObject({ contactPoint: { '@type': 'ContactPoint', telephone: '02112345678' } });
    expect(data).not.toHaveProperty('address');
    expect(data).not.toHaveProperty('sameAs');
    expect(data).not.toHaveProperty('telephone');
  });

  it('leaves out everything that is empty instead of inventing it', () => {
    const data = contactJsonLd({ phone: '', address: '', hours: '', instagram: '' });
    expect(Object.keys(data).sort()).toEqual(['@context', '@type', 'name', 'url']);
  });
});
