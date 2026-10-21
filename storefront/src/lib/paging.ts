/**
 * Reads the `?page=` query value. Absent means page 1; anything that is not a plain positive integer
 * (0, -1, 2.5, "abc", "01", huge numbers) is invalid and yields null so the page can answer 404.
 */
export function parsePageParam(value: string | undefined): number | null {
  if (value === undefined) return 1;
  return /^[1-9]\d{0,5}$/.test(value) ? Number(value) : null;
}

export type SearchParams = Record<string, string | string[] | undefined>;

/**
 * `?page=1` duplicates the listing's own URL, so it permanently redirects there. Returns the target URL
 * (other query params kept, in order), or null when no redirect is needed.
 */
export function pageOneRedirect(basePath: string, params: SearchParams): string | null {
  if (params.page !== '1') return null;
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (key === 'page' || value === undefined) continue;
    for (const item of Array.isArray(value) ? value : [value]) query.append(key, item);
  }
  const qs = query.toString();
  return qs ? `${basePath}?${qs}` : basePath;
}

/** A page is valid when it exists in the listing; an empty listing still has page 1. */
export function isPageInRange(page: number, pageCount: number): boolean {
  return page >= 1 && page <= Math.max(pageCount, 1);
}
