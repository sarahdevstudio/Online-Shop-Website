import type { SearchParams } from './paging';

/** Pure path helpers, free of React and Next imports so the proxy can use them too. */

const MAX_PATH = 200;
const MAX_SEGMENTS = 4;
const SEGMENT = /^[A-Za-z0-9]+(?:-[A-Za-z0-9]+)*$/;

/**
 * The shape a request path must have to be worth asking the API about (letters of either case, digits, single hyphens,
 * 1-4 segments): `/wp-login.php`, `/.env` or `/a//b` are 404s without a call. Mirrors the API's rule (case is folded there).
 */
export function isResolvablePath(path: string): boolean {
  if (path.length < 2 || path.length > MAX_PATH || path[0] !== '/') return false;
  const segments = path.slice(1).split('/');
  return segments.length <= MAX_SEGMENTS && segments.every((s) => SEGMENT.test(s));
}

/** `['product','x']` → `/product/x`. */
export function pathFromSegments(segments: readonly string[] | undefined): string {
  return `/${(segments ?? []).join('/')}`;
}

/** A redirect target must stay on this site: one leading slash, no scheme, no `//`, no backslash. */
export function isInternalPath(to: string): boolean {
  return /^\/(?![/\\])/.test(to) && !/[\u0000-\u001f\\]/.test(to);
}

/** The redirect URL: the current path plus the visitor's query string, untouched (filters, sort, page, variant choices). */
export function redirectUrl(to: string, query: SearchParams): string {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (Array.isArray(value)) value.forEach((v) => params.append(key, v));
    else if (value !== undefined) params.append(key, value);
  }
  const text = params.toString().replace(/%2C/gi, ','); // literal commas, as the listing URLs are written (`?wattage=12w,9w`)
  return text ? `${to}?${text}` : to;
}
