import { isProductionSite } from './site-env';

type Env = Record<string, string | undefined>;

/** Pages whose URL or content must never leak through a Referer header: the guest order page (token in the URL) and the admin. */
const NO_REFERRER = /^\/(order|admin)(\/|$)/;

/**
 * Response headers every storefront response gets (set by the proxy, at request time):
 * - X-Content-Type-Options: nosniff
 * - Referrer-Policy: strict-origin-when-cross-origin, but no-referrer on /order and /admin
 * - X-Frame-Options: DENY and CSP `frame-ancestors 'none'` (no framing at all; a full CSP is a Post-MVP item)
 * - Strict-Transport-Security: production only (HTTPS is terminated in front of Next.js)
 * - X-Robots-Tag: noindex, nofollow everywhere unless SITE_ENV=production
 */
export function securityHeaders(pathname: string, env: Env = process.env): Record<string, string> {
  const headers: Record<string, string> = {
    'X-Content-Type-Options': 'nosniff',
    'Referrer-Policy': NO_REFERRER.test(pathname) ? 'no-referrer' : 'strict-origin-when-cross-origin',
    'X-Frame-Options': 'DENY',
    'Content-Security-Policy': "frame-ancestors 'none'",
  };
  if (isProductionSite(env)) {
    headers['Strict-Transport-Security'] = 'max-age=31536000; includeSubDomains';
  } else {
    headers['X-Robots-Tag'] = 'noindex, nofollow';
  }
  return headers;
}
