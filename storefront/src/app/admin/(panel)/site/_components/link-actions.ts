'use server';

import { adminGet } from '@/lib/admin/api';
import { isLinkEntityType } from '@/lib/admin/link-model';
import type { LinkTarget } from '@/lib/admin/site-types';

/** The search behind the editor's "link to an entity" select (Site.Manage; the API checks it). At most 20 results. */
export async function searchLinkTargets(type: string, query: string): Promise<LinkTarget[]> {
  if (!isLinkEntityType(type)) return [];
  const text = query.trim().slice(0, 60);
  return (await adminGet<LinkTarget[]>(`/admin/site/link-targets?type=${encodeURIComponent(type)}&q=${encodeURIComponent(text)}`)) ?? [];
}
