import { readFileSync } from 'node:fs';
import { afterEach, describe, expect, it, vi } from 'vitest';

vi.mock('next/server', () => ({ connection: async () => undefined }));
process.env.API_BASE_URL = 'http://api.test';

import { DEFAULT_SITE_CONFIG, getSiteConfig, isSafeSiteLink, parseSiteConfig, titleFromTemplate } from '../src/lib/site-config';

const seed = JSON.parse(readFileSync(new URL('./fixtures/compiled-site.json', import.meta.url), 'utf8')) as Record<string, unknown>;

describe('parseSiteConfig', () => {
  it('reads the seeded document', () => {
    const config = parseSiteConfig(seed);
    expect(config.version).toBe('seed0001');
    expect(config.theme.primary).toBe('#5b0057');
    expect(config.theme.logo).toEqual({ url: '/media/site/logo.png', alt: 'قصر روشنایی', width: 300, height: 70 });
    expect(config.theme.favicon?.width).toBe(512);
    expect(config.home.blocks.map((b) => b.type)).toEqual(['hero', 'productRow', 'categoryTiles', 'text']);
    expect(config.chrome.headerMenu.map((m) => m.label)).toEqual(['خانه', 'فروشگاه', 'درباره ما', 'تماس']);
    expect(config.chrome.headerMenu[1].allCategories).toBe(true);
    expect(config.chrome.social.twitter).toBe('https://x.com/e_ghasr');
    expect(config.seo.siteName).toBe('قصر روشنایی');
    expect(titleFromTemplate(config.seo.titleTemplate, 'لوستر')).toBe('لوستر | قصر روشنایی');
    expect(config.badges).toEqual({ enamad: null, samandehi: null });
  });

  it('falls back field by field for partial documents', () => {
    const config = parseSiteConfig({ theme: { primary: 'nope', font: 'sahel' }, seo: { siteName: 'x' }, chrome: { announcement: { enabled: true, text: '' } } });
    expect(config.theme.primary).toBe(DEFAULT_SITE_CONFIG.theme.primary);
    expect(config.theme.font).toBe('sahel');
    expect(config.seo.siteName).toBe(DEFAULT_SITE_CONFIG.seo.siteName);
    expect(config.chrome.announcement.enabled).toBe(false);
    expect(config.home.blocks).toEqual([]);
  });

  it('returns the defaults for garbage', () => {
    for (const garbage of [null, undefined, 3, 'x', [], { home: 5, theme: 'x', chrome: [], seo: null, badges: 7 }]) {
      const config = parseSiteConfig(garbage);
      expect(config.theme.primary).toBe(DEFAULT_SITE_CONFIG.theme.primary);
      expect(config.home.blocks).toEqual([]);
      expect(config.chrome.headerMenu).toEqual([]);
      expect(config.badges.enamad).toBeNull();
    }
  });

  it('drops unknown or invalid blocks, bad links, foreign media and wrong social hosts', () => {
    const config = parseSiteConfig({
      home: { blocks: [
        { id: 'a', type: 'carousel3d', data: {} },
        { id: 'b', type: 'banner', data: { image: { url: 'https://evil.example/x.png', width: 10, height: 10 }, alt: 'x', height: 'md' } },
        { id: 'c', type: 'productRow', data: { source: 'category', limit: 8 } },
        { id: 'd', type: 'hero', data: { slides: [{ title: 'سلام', link: 'javascript:alert(1)', image: { url: '/media/../x.png', width: 1, height: 1 }, alt: 'x' }] } },
      ] },
      chrome: { headerMenu: [{ label: 'a', link: 'http://insecure.example' }, { label: 'b', link: '//evil.example' }], social: { twitter: 'https://evil.example/e_ghasr', aparat: 'https://www.aparat.com/x' } },
    });
    expect(config.home.blocks.map((b) => b.id)).toEqual(['d']);
    const hero = config.home.blocks[0];
    expect(hero.type === 'hero' && hero.data.slides[0]).toMatchObject({ image: null, link: null, title: 'سلام' });
    expect(config.chrome.headerMenu.every((m) => m.link === null)).toBe(true);
    expect(config.chrome.social).toEqual({ aparat: 'https://www.aparat.com/x' });
  });

  it('validates trust-badge identifiers, Samandehi with two codes', () => {
    const config = parseSiteConfig({ badges: { enamad: { id: '123456', code: 'AbCdEfGh12' }, samandehi: { id: '9', verifyCode: 'abcdefghij', logoCode: 'klmnopqrst' } } });
    expect(config.badges.enamad).toEqual({ id: '123456', code: 'AbCdEfGh12' });
    expect(config.badges.samandehi).toEqual({ id: '9', verifyCode: 'abcdefghij', logoCode: 'klmnopqrst' });
    const bad = parseSiteConfig({ badges: { enamad: { id: '12a', code: 'short' }, samandehi: { id: '9', code: 'abcdefghij' } } });
    expect(bad.badges).toEqual({ enamad: null, samandehi: null });
  });

  it('requires exactly one %s in the title template', () => {
    expect(parseSiteConfig({ seo: { titleTemplate: 'no placeholder' } }).seo.titleTemplate).toBe(DEFAULT_SITE_CONFIG.seo.titleTemplate);
    expect(parseSiteConfig({ seo: { titleTemplate: '%s | %s' } }).seo.titleTemplate).toBe(DEFAULT_SITE_CONFIG.seo.titleTemplate);
  });
});

describe('isSafeSiteLink', () => {
  it('accepts internal paths and https URLs only', () => {
    for (const ok of ['/', '/about', '/category/lamps?sort=newest', 'https://example.com/a']) expect(isSafeSiteLink(ok)).toBe(true);
    for (const bad of ['', 'about', '//evil.com', 'http://x.com', 'javascript:alert(1)', 'data:text/html,x', '/a b', "/a'b", 'https://', 5, null]) expect(isSafeSiteLink(bad)).toBe(false);
  });
});

describe('getSiteConfig', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('uses the published document from the API envelope', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response(JSON.stringify({ result: seed, isSuccess: true }), { status: 200 })));
    expect((await getSiteConfig()).theme.primary).toBe('#5b0057');
  });

  it('falls back to the defaults when the API is down, errors or answers garbage', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => { throw new Error('down'); }));
    expect(await getSiteConfig()).toBe(DEFAULT_SITE_CONFIG);
    vi.stubGlobal('fetch', vi.fn(async () => new Response('x', { status: 500 })));
    expect(await getSiteConfig()).toBe(DEFAULT_SITE_CONFIG);
    vi.stubGlobal('fetch', vi.fn(async () => new Response('not json', { status: 200 })));
    expect(await getSiteConfig()).toBe(DEFAULT_SITE_CONFIG);
  });
});
