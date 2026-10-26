import { cache } from 'react';
import { apiGet } from './api';

/** Mirrors the API's blog slug rule (lower-case English letters and digits, hyphen-separated); anything else is a 404. */
const SLUG = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

export function isBlogSlug(value: string): boolean {
  return value.length <= 100 && SLUG.test(value);
}

export interface BlogPostSummary {
  slug: string;
  title: string;
  excerpt: string;
  publishedAtUtc: string;
}

export interface BlogList {
  items: BlogPostSummary[];
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
}

export interface BlogPost {
  slug: string;
  title: string;
  excerpt: string;
  body: string;
  publishedAtUtc: string;
  updatedAtUtc: string;
}

export interface BlogSitemapEntry {
  slug: string;
  updatedAtUtc: string;
}

/** Seconds a post or list stays in Next's data cache; an admin edit shows up within this time. */
const REVALIDATE_SECONDS = 60;

/** One page of the list (newest first). API failures propagate (error page). */
export const getBlogList = cache(async (page: number): Promise<BlogList> =>
  (await apiGet<BlogList>(`/catalog/blog?page=${page}`, REVALIDATE_SECONDS)) ?? { items: [], page, pageSize: 12, total: 0, totalPages: 0 },
);

/** A published post; null for an unknown or unpublished slug. */
export const getBlogPost = cache(async (slug: string): Promise<BlogPost | null> =>
  apiGet<BlogPost>(`/catalog/blog/${encodeURIComponent(slug)}`, REVALIDATE_SECONDS),
);

/** Slug and last change of every published post, for the sitemap. */
export async function getBlogSitemap(): Promise<BlogSitemapEntry[]> {
  return (await apiGet<BlogSitemapEntry[]>('/catalog/blog/sitemap', REVALIDATE_SECONDS)) ?? [];
}
