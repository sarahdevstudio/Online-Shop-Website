/**
 * First path segments the storefront serves itself. The ONE list lives in the API (Shop.Domain.Routing.ReservedPaths: an admin
 * can never take these paths); this copy only lets the proxy skip route lookups for them, and tests/reserved-paths.test.ts
 * fails when the two differ.
 */
export const RESERVED_SEGMENTS = [
  '_next',
  'account',
  'admin',
  'api',
  'cart',
  'checkout',
  'favicon.ico',
  'fonts',
  'gone',
  'health',
  'media',
  'order',
  'robots.txt',
  'search',
  'site-icon',
  'sitemap.xml',
  'sitemaps',
] as const;

/** Whole paths (not prefixes) owned by static pages. */
export const RESERVED_EXACT = ['/blog'] as const;

export function isReservedPath(path: string): boolean {
  const lower = path.toLowerCase();
  if ((RESERVED_EXACT as readonly string[]).includes(lower.replace(/\/+$/, ''))) return true;
  const first = lower.split('/')[1] ?? '';
  return (RESERVED_SEGMENTS as readonly string[]).includes(first);
}
