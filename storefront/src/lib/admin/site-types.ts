import type { ContrastItem } from '../contrast';

/** Types of the admin site-configuration API (`/api/v1/admin/site/*`) and the five editable payloads. No server-only imports. */

export const SITE_KINDS = ['theme', 'home', 'chrome', 'seo', 'badges'] as const;
export type SiteKind = (typeof SITE_KINDS)[number];

export function isSiteKind(value: unknown): value is SiteKind {
  return typeof value === 'string' && (SITE_KINDS as readonly string[]).includes(value);
}

/** Where each kind is edited (the header/footer editor is the `chrome` kind). */
export const SITE_KIND_PATH: Record<SiteKind, string> = {
  theme: '/admin/site/theme',
  home: '/admin/site/home',
  chrome: '/admin/site/header-footer',
  seo: '/admin/site/seo',
  badges: '/admin/site/badges',
};

export interface ThemePayload {
  primary: string;
  secondary: string;
  accent: string;
  background: string;
  surface: string;
  text: string;
  success: string;
  warning: string;
  danger: string;
  font: 'vazirmatn' | 'estedad' | 'shabnam' | 'sahel';
  radius: 'none' | 'sm' | 'md' | 'lg';
  logo: { mediaId: number; alt: string } | null;
  favicon: { mediaId: number } | null;
}

export interface SeoPayload {
  siteName: string;
  titleTemplate: string;
  homeTitle: string;
  defaultDescription: string;
  defaultOgMediaId: number | null;
}

export interface BadgesPayload {
  enamad: { id: string; code: string } | null;
  samandehi: { id: string; verifyCode: string; logoCode: string } | null;
}

/** A link to an entity of the shop instead of a typed URL (the site document shows the entity's CURRENT path). */
export interface LinkEntity {
  type: LinkEntityType;
  id: number;
  /** The target's name when it was chosen: lets the editor still name it after it is deleted. Never shown on the site. */
  label?: string | null;
}

export const LINK_ENTITY_TYPES = ['category', 'product', 'contentPage', 'blogPost'] as const;
export type LinkEntityType = (typeof LINK_ENTITY_TYPES)[number];

/** What the editor knows about an entity a stored link points to (`links` of the document) or a search result. */
export interface LinkTarget {
  type: LinkEntityType;
  id: number;
  label: string;
  path: string | null;
  exists: boolean;
  /** Only `published` shows on the site; a draft, inactive or archived target hides the link until it is published again. */
  state: LinkState;
}

export const LINK_STATES = ['published', 'draft', 'inactive', 'archived', 'deleted'] as const;
export type LinkState = (typeof LINK_STATES)[number];

export interface HeroSlidePayload {
  mediaId: number | null;
  alt: string;
  title: string;
  text: string;
  linkLabel: string;
  link: string | null;
  entity?: LinkEntity | null;
}

export type HomeBlockPayload =
  | { id: string; type: 'hero'; enabled: boolean; data: { autoplaySeconds: number; slides: HeroSlidePayload[] } }
  | { id: string; type: 'categoryTiles'; enabled: boolean; data: { items: { categoryId: number; label: string | null }[] } }
  | {
      id: string;
      type: 'productRow';
      enabled: boolean;
      data: { title: string; source: 'newest' | 'discounted' | 'category' | 'handpicked'; categoryId: number | null; productIds: number[]; limit: number };
    }
  | { id: string; type: 'text'; enabled: boolean; data: { heading: string; body: string } }
  | { id: string; type: 'banner'; enabled: boolean; data: { mediaId: number; alt: string; link: string | null; entity?: LinkEntity | null; height: 'sm' | 'md' | 'lg' } };

export interface HomePayload {
  blocks: HomeBlockPayload[];
}

export interface MenuLinkPayload {
  label: string;
  link: string;
  entity?: LinkEntity | null;
}

export interface MenuItemPayload {
  label: string;
  link: string | null;
  entity?: LinkEntity | null;
  allCategories: boolean;
  children: MenuLinkPayload[];
}

export interface ChromePayload {
  announcement: { enabled: boolean; text: string; link: string | null; entity?: LinkEntity | null; style: 'primary' | 'accent' | 'neutral'; dismissible: boolean };
  headerMenu: MenuItemPayload[];
  footer: { about: string; columns: { title: string; links: MenuLinkPayload[] }[] };
  social: Record<'telegram' | 'whatsapp' | 'aparat' | 'twitter' | 'pinterest' | 'youtube' | 'linkedin', string | null>;
}

export interface PayloadByKind {
  theme: ThemePayload;
  home: HomePayload;
  chrome: ChromePayload;
  seo: SeoPayload;
  badges: BadgesPayload;
}

export interface SiteVersionSummary {
  version: number;
  state: 'published' | 'archived';
  publishedAtUtc: string;
  publishedBy: string | null;
}

export interface SiteMediaInfo {
  url: string;
  width: number;
  height: number;
  purpose: string;
}

/** `GET /admin/site/{kind}` (also the result of save / publish / discard / restore). */
export interface AdminSiteDocument<P> {
  kind: SiteKind;
  published: { version: number; payload: P; publishedAtUtc: string; publishedBy: string | null } | null;
  draft: { payload: P; rowVersion: string; updatedAtUtc: string; updatedBy: string | null } | null;
  history: SiteVersionSummary[];
  media: Record<string, SiteMediaInfo>;
  report: { contrast: ContrastItem[] } | null;
  /** Every entity the draft or the published payload links to, keyed `type:id`. */
  links?: Record<string, LinkTarget>;
}

export interface SiteOverviewItem {
  kind: SiteKind;
  hasDraft: boolean;
  draftUpdatedAtUtc: string | null;
  publishedVersion: number | null;
  publishedAtUtc: string | null;
}

export interface SiteVersionDocument<P> {
  version: number;
  state: 'published' | 'archived';
  payload: P;
  publishedAtUtc: string;
  publishedBy: string | null;
}

export type SiteAuditAction = 'saved' | 'published' | 'restored' | 'discarded' | 'uploaded' | 'converted';

export interface SiteAuditItem {
  atUtc: string;
  kind: SiteKind;
  action: SiteAuditAction;
  version: number | null;
  summary: string;
  adminUsername: string | null;
}

export interface SiteAuditPage {
  items: SiteAuditItem[];
  total: number;
  page: number;
  pageCount: number;
}

export interface SiteUploadResult {
  id: number;
  url: string;
  width: number;
  height: number;
  bytes: number;
  purpose: string;
  warnings?: string[];
}

export type SiteImagePurpose = 'logo' | 'favicon' | 'hero' | 'banner' | 'og';

/** Draft if there is one, else the published payload: what an editor starts from. */
export function effectivePayload<P>(doc: AdminSiteDocument<P>): P | null {
  return doc.draft?.payload ?? doc.published?.payload ?? null;
}
