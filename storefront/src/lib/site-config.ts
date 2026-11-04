import { cache } from 'react';
import { connection } from 'next/server';
import { apiBaseUrl } from './api';
import { fetchDraftDocument, readPreviewAuth } from './site-preview';
import { DEFAULT_THEME, sanitizeTheme, type ThemeValues } from './theme-css';
import { absoluteUrl, site } from './seo';

/**
 * The compiled site configuration (`GET /api/v1/catalog/site`, see docs/10-site-customization.md §11). Everything read from the
 * API is re-validated here field by field: a missing or invalid value becomes the built-in default for that field/section, and
 * an unreachable API means the built-in defaults — the storefront never renders a blank or broken page because of this file.
 */

// ---- types ----

export interface SiteImage {
  url: string;
  width: number;
  height: number;
}

export interface SiteLogo extends SiteImage {
  alt: string;
}

export interface SiteTheme extends ThemeValues {
  logo: SiteLogo | null;
  favicon: SiteImage | null;
}

export interface HeroSlide {
  image: SiteImage | null;
  alt: string;
  title: string;
  text: string;
  linkLabel: string;
  link: string | null;
}

export type ProductRowSource = 'newest' | 'discounted' | 'category' | 'handpicked';

export type HomeBlock =
  | { id: string; type: 'hero'; data: { autoplaySeconds: number; slides: HeroSlide[] } }
  | { id: string; type: 'categoryTiles'; data: { items: { label: string; slug: string; imageUrl: string | null }[] } }
  | {
      id: string;
      type: 'productRow';
      data: { title: string; source: ProductRowSource; limit: number; categorySlug: string | null; productSlugs: string[]; viewAllHref: string | null };
    }
  | { id: string; type: 'text'; data: { heading: string; body: string } }
  | { id: string; type: 'banner'; data: { image: SiteImage; alt: string; link: string | null; height: 'sm' | 'md' | 'lg' } };

export type AnnouncementStyle = 'primary' | 'accent' | 'neutral';

export interface Announcement {
  enabled: boolean;
  text: string;
  link: string | null;
  style: AnnouncementStyle;
  dismissible: boolean;
}

export interface MenuLink {
  label: string;
  link: string;
}

export interface MenuItem {
  label: string;
  link: string | null;
  allCategories: boolean;
  children: MenuLink[];
}

export interface FooterColumn {
  title: string;
  links: MenuLink[];
}

export const SOCIAL_PLATFORMS = ['telegram', 'whatsapp', 'aparat', 'twitter', 'pinterest', 'youtube', 'linkedin'] as const;
export type SocialPlatform = (typeof SOCIAL_PLATFORMS)[number];

export interface SiteSeo {
  siteName: string;
  titleTemplate: string;
  homeTitle: string;
  defaultDescription: string;
  defaultOgImage: SiteImage | null;
  /** The content of the Google Search Console verification meta tag; '' = no tag. */
  searchConsoleToken: string;
  /** A GA4 measurement id (G-XXXXXXXX); '' = no analytics is loaded at all. */
  ga4Id: string;
}

export interface Badges {
  enamad: { id: string; code: string } | null;
  samandehi: { id: string; verifyCode: string; logoCode: string } | null;
}

export interface SiteConfig {
  version: string;
  theme: SiteTheme;
  home: { blocks: HomeBlock[] };
  chrome: {
    announcement: Announcement;
    headerMenu: MenuItem[];
    footer: { about: string; columns: FooterColumn[] };
    social: Partial<Record<SocialPlatform, string>>;
  };
  seo: SiteSeo;
  badges: Badges;
}

/** Today's look and texts: used for any missing part and when the API cannot be reached. */
export const DEFAULT_SITE_CONFIG: SiteConfig = {
  version: 'default',
  theme: { ...DEFAULT_THEME, logo: null, favicon: null },
  home: { blocks: [] },
  chrome: {
    announcement: { enabled: false, text: '', link: null, style: 'primary', dismissible: true },
    headerMenu: [],
    footer: { about: site.description, columns: [] },
    social: {},
  },
  seo: {
    siteName: site.name,
    titleTemplate: `%s | ${site.name}`,
    homeTitle: `${site.name} | ${site.description}`,
    defaultDescription: site.description,
    defaultOgImage: null,
    searchConsoleToken: '',
    ga4Id: '',
  },
  badges: { enamad: null, samandehi: null },
};

// ---- small validators ----

type Raw = Record<string, unknown>;

const isObject = (value: unknown): value is Raw => typeof value === 'object' && value !== null && !Array.isArray(value);

function text(value: unknown, max: number, { min = 0, trim = true } = {}): string | null {
  if (typeof value !== 'string') return null;
  const result = trim ? value.trim() : value;
  return result.length >= min && result.length <= max ? result : null;
}

function int(value: unknown, min: number, max: number): number | null {
  return typeof value === 'number' && Number.isInteger(value) && value >= min && value <= max ? value : null;
}

const INTERNAL_PATH = /^\/[A-Za-z0-9\-._~%/?=&#]*$/;

/** An internal path (single leading `/`) or an absolute `https://` URL; anything else (javascript:, data:, http:, //host) is refused. */
export function isSafeSiteLink(value: unknown): value is string {
  if (typeof value !== 'string' || value.length === 0 || value.length > 300) return false;
  if (value.startsWith('/')) return !value.startsWith('//') && INTERNAL_PATH.test(value);
  if (!/^https:\/\//i.test(value)) return false;
  try {
    const url = new URL(value);
    return url.protocol === 'https:' && url.hostname.length > 0 && !/[\s<>"']/.test(value);
  } catch {
    return false;
  }
}

const link = (value: unknown): string | null => (isSafeSiteLink(value) ? value : null);

const MEDIA_URL = /^\/media\/[A-Za-z0-9/_.-]+$/;

function image(value: unknown): SiteImage | null {
  if (!isObject(value) || typeof value.url !== 'string' || !MEDIA_URL.test(value.url) || value.url.includes('..')) return null;
  const width = int(value.width, 1, 20000);
  const height = int(value.height, 1, 20000);
  return width && height ? { url: value.url, width, height } : null;
}

function list(value: unknown, max: number): unknown[] {
  return Array.isArray(value) ? value.slice(0, max) : [];
}

// ---- sections ----

function parseTheme(raw: unknown): SiteTheme {
  const base = sanitizeTheme(raw);
  const source = isObject(raw) ? raw : {};
  let logo: SiteLogo | null = null;
  const logoImage = image(source.logo);
  const logoAlt = isObject(source.logo) ? text(source.logo.alt, 100, { min: 1 }) : null;
  if (logoImage && logoAlt) logo = { ...logoImage, alt: logoAlt };
  return { ...base, logo, favicon: image(source.favicon) };
}

function parseSlide(raw: unknown): HeroSlide | null {
  if (!isObject(raw)) return null;
  const title = text(raw.title, 80) ?? '';
  const body = text(raw.text, 200) ?? '';
  const img = image(raw.image);
  const alt = text(raw.alt, 150) ?? '';
  if (!img && !title && !body) return null;
  return { image: img && alt ? img : null, alt, title, text: body, linkLabel: text(raw.linkLabel, 30) ?? '', link: link(raw.link) };
}

function parseBlock(raw: unknown): HomeBlock | null {
  if (!isObject(raw) || !isObject(raw.data)) return null;
  const id = text(raw.id, 64, { min: 1 });
  if (!id) return null;
  const data = raw.data;
  switch (raw.type) {
    case 'hero': {
      const slides = list(data.slides, 6).map(parseSlide).filter((s): s is HeroSlide => s !== null);
      if (slides.length === 0) return null;
      const seconds = int(data.autoplaySeconds, 0, 12);
      return { id, type: 'hero', data: { autoplaySeconds: seconds === 0 || (seconds !== null && seconds >= 4) ? seconds : 0, slides } };
    }
    case 'categoryTiles': {
      const items = list(data.items, 12).flatMap((item) => {
        if (!isObject(item)) return [];
        const slug = text(item.slug, 100, { min: 1 });
        const label = text(item.label, 40, { min: 1 });
        if (!slug || !label || !/^[a-z0-9][a-z0-9-]*$/.test(slug)) return [];
        const imageUrl = typeof item.imageUrl === 'string' && MEDIA_URL.test(item.imageUrl) ? item.imageUrl : null;
        return [{ label, slug, imageUrl }];
      });
      return items.length > 0 ? { id, type: 'categoryTiles', data: { items } } : null;
    }
    case 'productRow': {
      const source = (['newest', 'discounted', 'category', 'handpicked'] as const).find((s) => s === data.source);
      if (!source) return null;
      const slugs = list(data.productSlugs, 12).filter((s): s is string => typeof s === 'string' && /^[a-z0-9][a-z0-9-]*$/.test(s));
      const categorySlug = typeof data.categorySlug === 'string' && /^[a-z0-9][a-z0-9-]*$/.test(data.categorySlug) ? data.categorySlug : null;
      if (source === 'category' && !categorySlug) return null;
      if (source === 'handpicked' && slugs.length === 0) return null;
      return {
        id,
        type: 'productRow',
        data: { title: text(data.title, 60) ?? '', source, limit: int(data.limit, 4, 12) ?? 8, categorySlug, productSlugs: slugs, viewAllHref: link(data.viewAllHref) },
      };
    }
    case 'text': {
      const body = text(data.body, 3000, { min: 1 });
      return body ? { id, type: 'text', data: { heading: text(data.heading, 80) ?? '', body } } : null;
    }
    case 'banner': {
      const img = image(data.image);
      const alt = text(data.alt, 150, { min: 1 });
      const height = (['sm', 'md', 'lg'] as const).find((h) => h === data.height) ?? 'md';
      return img && alt ? { id, type: 'banner', data: { image: img, alt, link: link(data.link), height } } : null;
    }
    default:
      return null; // unknown types are dropped (forward compatibility)
  }
}

function parseMenuLinks(raw: unknown, max: number): MenuLink[] {
  return list(raw, max).flatMap((item) => {
    if (!isObject(item)) return [];
    const label = text(item.label, 30, { min: 1 });
    const target = link(item.link);
    return label && target ? [{ label, link: target }] : [];
  });
}

function parseHeaderMenu(raw: unknown): MenuItem[] {
  return list(raw, 8).flatMap((item) => {
    if (!isObject(item)) return [];
    const label = text(item.label, 30, { min: 1 });
    if (!label) return [];
    const allCategories = item.allCategories === true;
    return [{ label, link: link(item.link), allCategories, children: allCategories ? [] : parseMenuLinks(item.children, 8) }];
  });
}

const SOCIAL_HOSTS: Record<SocialPlatform, readonly string[]> = {
  telegram: ['t.me', 'telegram.me'],
  whatsapp: ['wa.me', 'api.whatsapp.com'],
  aparat: ['aparat.com', 'www.aparat.com'],
  twitter: ['twitter.com', 'x.com', 'www.twitter.com', 'www.x.com'],
  pinterest: ['pinterest.com', 'www.pinterest.com'],
  youtube: ['youtube.com', 'www.youtube.com'],
  linkedin: ['linkedin.com', 'www.linkedin.com'],
};

function parseSocial(raw: unknown): Partial<Record<SocialPlatform, string>> {
  const result: Partial<Record<SocialPlatform, string>> = {};
  if (!isObject(raw)) return result;
  for (const platform of SOCIAL_PLATFORMS) {
    const value = raw[platform];
    if (typeof value !== 'string' || !value.startsWith('https://') || !isSafeSiteLink(value)) continue;
    if (SOCIAL_HOSTS[platform].includes(new URL(value).hostname.toLowerCase())) result[platform] = value;
  }
  return result;
}

function parseChrome(raw: unknown): SiteConfig['chrome'] {
  const source = isObject(raw) ? raw : {};
  const a = isObject(source.announcement) ? source.announcement : {};
  const announcementText = text(a.text, 140) ?? '';
  const style = (['primary', 'accent', 'neutral'] as const).find((s) => s === a.style) ?? 'primary';
  const footer = isObject(source.footer) ? source.footer : {};
  const columns = list(footer.columns, 4).flatMap((column) => {
    if (!isObject(column)) return [];
    const title = text(column.title, 40, { min: 1 });
    const links = parseMenuLinks(column.links, 8);
    return title && links.length > 0 ? [{ title, links }] : [];
  });
  return {
    announcement: { enabled: a.enabled === true && announcementText.length > 0, text: announcementText, link: link(a.link), style, dismissible: a.dismissible !== false },
    headerMenu: parseHeaderMenu(source.headerMenu),
    footer: { about: text(footer.about, 300) ?? DEFAULT_SITE_CONFIG.chrome.footer.about, columns },
    social: parseSocial(source.social),
  };
}

function parseSeo(raw: unknown): SiteSeo {
  const source = isObject(raw) ? raw : {};
  const defaults = DEFAULT_SITE_CONFIG.seo;
  const template = text(source.titleTemplate, 70, { min: 2 });
  return {
    siteName: text(source.siteName, 60, { min: 2 }) ?? defaults.siteName,
    titleTemplate: template && template.split('%s').length === 2 ? template : defaults.titleTemplate,
    homeTitle: text(source.homeTitle, 70, { min: 1 }) ?? defaults.homeTitle,
    defaultDescription: text(source.defaultDescription, 160, { min: 1 }) ?? defaults.defaultDescription,
    defaultOgImage: image(source.defaultOgImage),
    // Same shapes the API enforces; anything else is dropped, so a bad value can never reach the page markup.
    searchConsoleToken: typeof source.searchConsoleToken === 'string' && SEARCH_CONSOLE_TOKEN.test(source.searchConsoleToken) ? source.searchConsoleToken : '',
    ga4Id: typeof source.ga4Id === 'string' && GA4_ID.test(source.ga4Id) ? source.ga4Id : '',
  };
}

export const SEARCH_CONSOLE_TOKEN = /^[A-Za-z0-9_-]{20,100}$/;
export const GA4_ID = /^G-[A-Z0-9]{6,12}$/;

const ID = /^[0-9]{1,10}$/;
const CODE = /^[A-Za-z0-9]{10,64}$/;

function parseBadges(raw: unknown): Badges {
  const source = isObject(raw) ? raw : {};
  const en = isObject(source.enamad) ? source.enamad : null;
  const sa = isObject(source.samandehi) ? source.samandehi : null;
  return {
    enamad: en && typeof en.id === 'string' && ID.test(en.id) && typeof en.code === 'string' && CODE.test(en.code) ? { id: en.id, code: en.code } : null,
    samandehi:
      sa && typeof sa.id === 'string' && ID.test(sa.id) && typeof sa.verifyCode === 'string' && CODE.test(sa.verifyCode) && typeof sa.logoCode === 'string' && CODE.test(sa.logoCode)
        ? { id: sa.id, verifyCode: sa.verifyCode, logoCode: sa.logoCode }
        : null,
  };
}

/** The whole document, validated section by section; never throws. */
export function parseSiteConfig(raw: unknown): SiteConfig {
  if (!isObject(raw)) return DEFAULT_SITE_CONFIG;
  const home = isObject(raw.home) ? raw.home : {};
  return {
    version: text(raw.version, 64) ?? 'unknown',
    theme: parseTheme(raw.theme),
    home: { blocks: list(home.blocks, 20).map(parseBlock).filter((b): b is HomeBlock => b !== null) },
    chrome: parseChrome(raw.chrome),
    seo: parseSeo(raw.seo),
    badges: parseBadges(raw.badges),
  };
}

/** `%s | قصر روشنایی` + a page title. */
export function titleFromTemplate(template: string, title: string): string {
  return template.replace('%s', title);
}

export const SITE_CACHE_TAG = 'site';

/** Seconds a published configuration stays in Next's data cache; publishing also calls revalidateTag('site'). */
const REVALIDATE_SECONDS = 60;
const TIMEOUT_MS = 5_000;

/**
 * The draft configuration for an admin who started a preview, or null (no valid preview cookie, or the API refused it: the
 * preview then silently turns itself off). Never cached.
 */
export const getPreviewConfig = cache(async (): Promise<SiteConfig | null> => {
  const auth = await readPreviewAuth();
  if (!auth) return null;
  const document = await fetchDraftDocument(auth);
  return document ? parseSiteConfig(document) : null;
});

/** The published configuration (or the draft while previewing); any failure means the built-in defaults. One API call per render. */
export const getSiteConfig = cache(async (): Promise<SiteConfig> => {
  try {
    await connection();
    const preview = await getPreviewConfig();
    if (preview) return preview;
    const response = await fetch(`${apiBaseUrl()}/api/v1/catalog/site`, {
      headers: { accept: 'application/json' },
      next: { revalidate: REVALIDATE_SECONDS, tags: [SITE_CACHE_TAG] },
      signal: AbortSignal.timeout(TIMEOUT_MS),
    });
    if (!response.ok) return DEFAULT_SITE_CONFIG;
    const envelope = (await response.json()) as { result?: unknown };
    return parseSiteConfig(envelope.result);
  } catch {
    return DEFAULT_SITE_CONFIG;
  }
});

/** Site name and fallback share image (absolute URL) from the SEO defaults, for pages that build their own metadata. */
export async function seoDefaults(): Promise<{ siteName: string; image: string | null }> {
  const { seo } = await getSiteConfig();
  return { siteName: seo.siteName, image: seo.defaultOgImage ? absoluteUrl(seo.defaultOgImage.url) : null };
}
