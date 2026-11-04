import type { CspExtra } from './csp';
import { GA4_ID } from './site-config';

/**
 * Whether the policy must name Google Tag Manager / Analytics: only while a GA4 id is configured. The proxy asks on every page
 * request, so the answer is kept in memory for a short while (an id the owner just set or removed takes effect within it) and
 * any trouble reaching the API means "no analytics": the CSP then stays at its strictest.
 */
const TTL_MS = 30_000;
let cached: { at: number; id: string } | null = null;
let pausedUntil = 0;

async function configuredGa4Id(): Promise<string> {
  const now = Date.now();
  if (cached && now - cached.at < TTL_MS) return cached.id;
  if (now < pausedUntil) return cached?.id ?? '';
  const base = process.env.API_BASE_URL ?? (process.env.NODE_ENV === 'development' ? 'http://localhost:5167' : '');
  if (!base) return '';
  try {
    const response = await fetch(`${base.replace(/\/+$/, '')}/api/v1/catalog/site`, { headers: { accept: 'application/json' }, cache: 'no-store', signal: AbortSignal.timeout(1500) });
    if (!response.ok) throw new Error(`site returned ${response.status}`);
    const body = (await response.json()) as { result?: { seo?: { ga4Id?: unknown } } };
    const id = typeof body.result?.seo?.ga4Id === 'string' && GA4_ID.test(body.result.seo.ga4Id) ? body.result.seo.ga4Id : '';
    cached = { at: now, id };
    return id;
  } catch {
    pausedUntil = now + 5_000;
    return cached?.id ?? '';
  }
}

/** What to add to the CSP for the analytics tag, or undefined when none is configured. */
export async function analyticsCspExtra(): Promise<CspExtra | undefined> {
  if (!(await configuredGa4Id())) return undefined;
  return {
    script: ['https://www.googletagmanager.com'],
    connect: ['https://www.google-analytics.com', 'https://*.google-analytics.com', 'https://*.analytics.google.com', 'https://www.googletagmanager.com'],
    img: ['https://www.google-analytics.com', 'https://*.google-analytics.com', 'https://www.googletagmanager.com'],
  };
}
