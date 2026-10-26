import { cache } from 'react';
import { apiGet } from './api';

/** The fixed info pages (see the API's ContentSlugs). */
export const INFO_SLUGS = ['about', 'contact', 'terms', 'returns', 'privacy'] as const;
export type InfoSlug = (typeof INFO_SLUGS)[number];

export function isInfoSlug(value: string): value is InfoSlug {
  return (INFO_SLUGS as readonly string[]).includes(value);
}

/** Link text used when the API can't be reached (the footer must never break a page). */
export const DEFAULT_TITLES: Record<InfoSlug, string> = {
  about: 'دربارهٔ ما',
  contact: 'تماس با ما',
  terms: 'شرایط و قوانین',
  returns: 'مرجوعی و بازگشت کالا',
  privacy: 'حریم خصوصی',
};

export interface ContentPageDto {
  slug: string;
  title: string;
  body: string;
  updatedAtUtc: string;
}

export interface ContentPageSummary {
  slug: string;
  title: string;
  updatedAtUtc: string;
}

/** Seconds the pages stay in Next's data cache; an admin edit shows up within this time. */
const REVALIDATE_SECONDS = 60;

/** One page; null for a slug the API doesn't know. API failures propagate (error page). */
export const getContentPage = cache(async (slug: InfoSlug): Promise<ContentPageDto | null> =>
  apiGet<ContentPageDto>(`/catalog/pages/${slug}`, REVALIDATE_SECONDS),
);

/** Slug, title and last change of every page, for the sitemap. */
export async function getContentPages(): Promise<ContentPageSummary[]> {
  return (await apiGet<ContentPageSummary[]>('/catalog/pages', REVALIDATE_SECONDS)) ?? [];
}

/** Footer links: titles from the API, or the default titles when it is unavailable. */
export async function getInfoLinks(): Promise<{ label: string; href: string }[]> {
  let titles: Record<string, string> = {};
  try {
    titles = Object.fromEntries((await getContentPages()).map((p) => [p.slug, p.title]));
  } catch {
    // keep the defaults
  }
  return INFO_SLUGS.map((slug) => ({ label: titles[slug] ?? DEFAULT_TITLES[slug], href: `/${slug}` }));
}
