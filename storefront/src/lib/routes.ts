import { cache } from 'react';
import { apiGet } from './api';
import { isInternalPath, isResolvablePath } from './route-paths';

/**
 * Dynamic routing, storefront side (docs/11 C.4). The API owns the route table; a path is resolved by one cached call and the
 * answer says what to render, where to redirect (always ONE hop, to the current path) or that nothing is there (404/410).
 */

export type EntityKind = 'category' | 'product' | 'contentPage' | 'blogPost';

export type Resolution =
  | { kind: EntityKind; id: number; slug: string; path: string }
  | { kind: 'redirect'; to: string; status: 308 }
  | { kind: 'gone' }
  | { kind: 'notFound' };

/** Data-cache tag of every resolve call; the admin action that changes a path invalidates it. */
export const ROUTES_TAG = 'routes';

/** Seconds a resolve answer is cached. The canonical tag already points at the new path, so a stale answer is harmless. */
export const ROUTES_REVALIDATE_SECONDS = 60;

/** One API call per distinct path per render; the Next data cache keeps the answer for a minute (tag `routes`). */
export const resolvePath = cache(async (path: string): Promise<Resolution> => {
  if (!isResolvablePath(path)) return { kind: 'notFound' };
  const resolution = await apiGet<Resolution>(
    `/catalog/resolve?path=${encodeURIComponent(path)}`,
    ROUTES_REVALIDATE_SECONDS,
    [ROUTES_TAG],
  );
  if (!resolution) return { kind: 'notFound' };
  if (resolution.kind === 'redirect' && !isInternalPath(resolution.to)) return { kind: 'notFound' };
  return resolution;
});

export { isInternalPath, isResolvablePath, pathFromSegments, redirectUrl } from './route-paths';
