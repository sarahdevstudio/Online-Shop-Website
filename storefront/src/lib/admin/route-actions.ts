import { revalidatePath, updateTag } from 'next/cache';
import { adminFetch, type AdminResult } from './api';
import { text } from './form-input';
import { failState, problemState, type FormState } from './form-state';
import { adminMessages } from './messages';
import { normalizePathInput, pathProblemState } from './route-form';
import type { RouteTarget } from './types';
import { CATALOG_TAG } from '../api';
import { ROUTES_TAG } from '../routes';
import { SITE_CACHE_TAG } from '../site-config';

/**
 * Changing an entity's public path from its edit form (docs/11 C.3, decision C-4: the entity's own permission is enough; the API
 * checks it). Used by the entity actions BEFORE they save the entity, so a refused path leaves everything untouched.
 */

export type RouteType = RouteTarget['type'];

export type PathChange = { ok: true; changed: boolean } | { ok: false; state: FormState };

export interface PathChangeDeps {
  put: (url: string, body: { path: string; rowVersion: string }) => Promise<AdminResult<RouteTarget>>;
  /** Drops everything cached that may show a path: resolve answers (`routes`), DTOs with `path` (`catalog`), the site document (menu, footer and home links) and the sitemap. */
  refresh: () => void;
}

/** A link on the site shows only while its target is published, so publishing or hiding a product or an info page refreshes the site document. */
export function refreshSiteLinks() {
  updateTag(SITE_CACHE_TAG);
}

export function refreshPublicPaths() {
  updateTag(ROUTES_TAG);
  updateTag(CATALOG_TAG);
  updateTag(SITE_CACHE_TAG); // the menu, footer and home links of the site document follow the entity's path
  revalidatePath('/sitemap.xml');
}

const realDeps: PathChangeDeps = {
  put: (url, body) => adminFetch<RouteTarget>('PUT', url, { body }),
  refresh: refreshPublicPaths,
};

/**
 * Reads `path`, `currentPath` and `path_rowVersion` from the form. Nothing is sent when the field is unchanged, so saving a
 * product never touches its route unless the admin edited the path.
 */
export async function applyPathChange(
  type: RouteType,
  id: number,
  formData: FormData,
  submitted: Record<string, string>,
  deps: PathChangeDeps = realDeps,
): Promise<PathChange> {
  const typed = text(formData, 'path');
  if (normalizePathInput(typed) === normalizePathInput(text(formData, 'currentPath'))) return { ok: true, changed: false };

  const result = await deps.put(`/admin/routes/${type}/${id}`, { path: typed, rowVersion: text(formData, 'path_rowVersion') });
  if (!result.ok) {
    if (result.status === 403) return { ok: false, state: failState(adminMessages.forbidden, submitted) };
    const state = pathProblemState(result.problem, submitted) ?? problemState(result.problem, submitted);
    return { ok: false, state };
  }

  deps.refresh();
  return { ok: true, changed: true };
}
