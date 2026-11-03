/**
 * Pure model of the header/footer/announcement/social draft (payload of `/admin/site/chrome`, docs/10 §6) and the list
 * helpers of the editor. The server validates everything again; these limits only keep the editor usable.
 */

export const CHROME_LIMITS = {
  announcementText: 140,
  menuItems: 8,
  menuChildren: 8,
  menuLabel: 30,
  footerColumns: 4,
  footerLinks: 8,
  footerTitle: 40,
  footerAbout: 300,
} as const;

export const ANNOUNCEMENT_STYLES = ['primary', 'accent', 'neutral'] as const;
export type AnnouncementStyle = (typeof ANNOUNCEMENT_STYLES)[number];

/** Social platforms the admin can fill (Instagram lives in the store settings). */
export const SOCIAL_PLATFORMS = ['telegram', 'whatsapp', 'aparat', 'twitter', 'pinterest', 'youtube', 'linkedin'] as const;
export type SocialPlatform = (typeof SOCIAL_PLATFORMS)[number];

export interface Announcement {
  enabled: boolean;
  text: string;
  link: string | null;
  style: AnnouncementStyle;
  dismissible: boolean;
}
export interface MenuChild {
  label: string;
  link: string;
}
export interface MenuItem {
  label: string;
  link: string | null;
  allCategories: boolean;
  children: MenuChild[];
}
export interface FooterLink {
  label: string;
  link: string;
}
export interface FooterColumn {
  title: string;
  links: FooterLink[];
}
export interface ChromePayload {
  announcement: Announcement;
  headerMenu: MenuItem[];
  footer: { about: string; columns: FooterColumn[] };
  social: Record<SocialPlatform, string | null>;
}

export const newMenuItem = (): MenuItem => ({ label: '', link: '/', allCategories: false, children: [] });
export const newMenuChild = (): MenuChild => ({ label: '', link: '/' });
export const newFooterColumn = (): FooterColumn => ({ title: '', links: [{ label: '', link: '/' }] });
export const newFooterLink = (): FooterLink => ({ label: '', link: '/' });

/** The stored payload with every part present (a draft saved by an older client may miss optional parts). */
export function normalizeChrome(raw: Partial<ChromePayload> | null | undefined): ChromePayload {
  const social = Object.fromEntries(SOCIAL_PLATFORMS.map((p) => [p, raw?.social?.[p] ?? null])) as ChromePayload['social'];
  return {
    announcement: {
      enabled: raw?.announcement?.enabled ?? false,
      text: raw?.announcement?.text ?? '',
      link: raw?.announcement?.link ?? null,
      style: raw?.announcement?.style ?? 'primary',
      dismissible: raw?.announcement?.dismissible ?? true,
    },
    headerMenu: raw?.headerMenu ?? [],
    footer: { about: raw?.footer?.about ?? '', columns: raw?.footer?.columns ?? [] },
    social,
  };
}

/** Empty strings in social inputs mean "hidden": the server stores null. */
export function socialForSave(values: Readonly<Record<SocialPlatform, string>>): ChromePayload['social'] {
  return Object.fromEntries(SOCIAL_PLATFORMS.map((p) => [p, values[p].trim() === '' ? null : values[p].trim()])) as ChromePayload['social'];
}

export function parseChromePayload(json: string): ChromePayload | null {
  try {
    const value = JSON.parse(json) as Partial<ChromePayload> | null;
    return value && typeof value === 'object' ? normalizeChrome(value) : null;
  } catch {
    return null;
  }
}
