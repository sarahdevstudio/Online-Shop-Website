/**
 * Which deployment this is. Only `production` may be indexed by search engines; `staging`, `development`, an unset value
 * and any typo all count as NOT production, so the safe default is "do not index". Read per request (not at build time), so
 * one build can be deployed to staging first and production later.
 */
export type SiteEnv = 'production' | 'staging' | 'development';

type Env = Record<string, string | undefined>;

export function siteEnv(env: Env = process.env): SiteEnv {
  const value = env.SITE_ENV?.trim().toLowerCase();
  return value === 'production' || value === 'staging' ? value : 'development';
}

export const isProductionSite = (env: Env = process.env): boolean => siteEnv(env) === 'production';

function isPublicHttpsUrl(value: string | undefined): boolean {
  if (!value) return false;
  try {
    const url = new URL(value);
    const host = url.hostname.toLowerCase();
    return url.protocol === 'https:' && host !== 'localhost' && host !== '127.0.0.1' && host !== '[::1]' && !host.endsWith('.localhost') && host.includes('.');
  } catch {
    return false;
  }
}

function isHttpUrl(value: string | undefined): boolean {
  if (!value) return false;
  try {
    return ['http:', 'https:'].includes(new URL(value).protocol);
  } catch {
    return false;
  }
}

/**
 * What a production deployment (SITE_ENV=production) must have before it builds or starts. Canonical URLs, the sitemap and
 * structured data are built from NEXT_PUBLIC_SITE_URL, so a localhost fallback there would publish wrong links.
 * Other environments have no requirements. Empty list = fine.
 */
export function productionEnvProblems(env: Env = process.env): string[] {
  if (!isProductionSite(env)) return [];
  const problems: string[] = [];
  if (!isPublicHttpsUrl(env.NEXT_PUBLIC_SITE_URL)) {
    problems.push('NEXT_PUBLIC_SITE_URL must be the public https origin of the shop (for example https://shop.example.ir); it is used for canonical URLs, the sitemap and structured data.');
  }
  if (!isHttpUrl(env.API_BASE_URL)) {
    problems.push('API_BASE_URL must be the private URL of the API (for example http://127.0.0.1:5167).');
  }
  return problems;
}

/** Throws with every problem listed. Called by next.config.ts, so `next build` and `next start` both refuse to go on. */
export function requireProductionEnv(env: Env = process.env): void {
  const problems = productionEnvProblems(env);
  if (problems.length > 0) {
    throw new Error(`SITE_ENV=production but the environment is incomplete:\n- ${problems.join('\n- ')}`);
  }
}
