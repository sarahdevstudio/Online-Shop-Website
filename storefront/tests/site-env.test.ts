import { NextRequest } from 'next/server';
import { afterEach, describe, expect, it, vi } from 'vitest';
import robots from '../src/app/robots';
import { productionEnvProblems, requireProductionEnv, siteEnv, isProductionSite } from '../src/lib/site-env';
import { securityHeaders } from '../src/lib/security-headers';
import { proxy } from '../src/proxy';
import nextConfig from '../next.config';

afterEach(() => vi.unstubAllEnvs());

const GOOD = { SITE_ENV: 'production', NEXT_PUBLIC_SITE_URL: 'https://shop.example.ir', API_BASE_URL: 'http://127.0.0.1:5167' };

describe('siteEnv', () => {
  it.each([
    ['production', 'production'],
    ['PRODUCTION', 'production'],
    [' staging ', 'staging'],
    ['development', 'development'],
    [undefined, 'development'], // unset: the safe default is "do not index"
    ['', 'development'],
    ['prod', 'development'], // a typo must never make the site indexable
    ['live', 'development'],
  ])('SITE_ENV=%j → %s', (value, expected) => {
    expect(siteEnv({ SITE_ENV: value })).toBe(expected);
    expect(isProductionSite({ SITE_ENV: value })).toBe(expected === 'production');
  });
});

describe('production environment requirements', () => {
  it('a complete production environment has no problems', () => {
    expect(productionEnvProblems(GOOD)).toEqual([]);
    expect(() => requireProductionEnv(GOOD)).not.toThrow();
  });

  it.each([
    ['missing site URL', { NEXT_PUBLIC_SITE_URL: undefined }],
    ['empty site URL', { NEXT_PUBLIC_SITE_URL: '' }],
    ['localhost site URL', { NEXT_PUBLIC_SITE_URL: 'https://localhost:3000' }],
    ['http site URL', { NEXT_PUBLIC_SITE_URL: 'http://shop.example.ir' }],
    ['loopback site URL', { NEXT_PUBLIC_SITE_URL: 'https://127.0.0.1' }],
    ['not a URL', { NEXT_PUBLIC_SITE_URL: 'shop.example.ir' }],
    ['site URL without a dot', { NEXT_PUBLIC_SITE_URL: 'https://shop' }],
  ])('refuses %s', (_name, change) => {
    const env = { ...GOOD, ...change };
    expect(productionEnvProblems(env).join(' ')).toContain('NEXT_PUBLIC_SITE_URL');
    expect(() => requireProductionEnv(env)).toThrow(/NEXT_PUBLIC_SITE_URL/);
  });

  it.each([undefined, '', 'not a url', 'ftp://api.internal'])('refuses API_BASE_URL=%j', (api) => {
    const env = { ...GOOD, API_BASE_URL: api };
    expect(() => requireProductionEnv(env)).toThrow(/API_BASE_URL/);
  });

  it('lists every problem at once', () => {
    expect(productionEnvProblems({ SITE_ENV: 'production' })).toHaveLength(2);
  });

  it('staging and development have no requirements', () => {
    expect(productionEnvProblems({ SITE_ENV: 'staging' })).toEqual([]);
    expect(productionEnvProblems({})).toEqual([]);
  });

  it('next.config.ts has already enforced it for this environment (build and start both load it)', () => {
    expect(nextConfig).toBeDefined(); // importing it ran requireProductionEnv() with the test environment (not production)
  });
});

describe('security headers', () => {
  it('every response gets nosniff, a referrer policy, no framing and a frame-ancestors CSP', () => {
    const h = securityHeaders('/product/led-bulb', GOOD);
    expect(h['X-Content-Type-Options']).toBe('nosniff');
    expect(h['Referrer-Policy']).toBe('strict-origin-when-cross-origin');
    expect(h['X-Frame-Options']).toBe('DENY');
    expect(h['Content-Security-Policy']).toBe("frame-ancestors 'none'");
  });

  it('the order page and the admin send no referrer at all', () => {
    for (const path of ['/order/abc123', '/order', '/admin', '/admin/orders/5']) {
      expect(securityHeaders(path, GOOD)['Referrer-Policy'], path).toBe('no-referrer');
    }
    // a page that merely starts with the same letters is a normal page
    expect(securityHeaders('/orders-guide', GOOD)['Referrer-Policy']).toBe('strict-origin-when-cross-origin');
    expect(securityHeaders('/administrator', GOOD)['Referrer-Policy']).toBe('strict-origin-when-cross-origin');
  });

  it('HSTS only in production; noindex only outside production', () => {
    const production = securityHeaders('/', GOOD);
    expect(production['Strict-Transport-Security']).toBe('max-age=31536000; includeSubDomains');
    expect(production['X-Robots-Tag']).toBeUndefined();

    for (const env of [{ SITE_ENV: 'staging' }, { SITE_ENV: 'development' }, {}]) {
      const other = securityHeaders('/', env);
      expect(other['Strict-Transport-Security']).toBeUndefined();
      expect(other['X-Robots-Tag']).toBe('noindex, nofollow');
    }
  });
});

describe('robots.txt', () => {
  it('production: allows crawling, keeps private pages out, points to the sitemap', () => {
    vi.stubEnv('SITE_ENV', 'production');
    vi.stubEnv('NEXT_PUBLIC_SITE_URL', 'https://shop.example.ir');

    const result = robots();
    const rule = Array.isArray(result.rules) ? result.rules[0] : result.rules;

    expect(rule.allow).toBe('/');
    expect(rule.disallow).toEqual(expect.arrayContaining(['/cart', '/checkout', '/order', '/admin']));
    expect(result.sitemap).toMatch(/\/sitemap\.xml$/);
  });

  it.each(['staging', 'development', '', 'typo'])('SITE_ENV=%j: disallows everything and offers no sitemap', (value) => {
    vi.stubEnv('SITE_ENV', value);

    const result = robots();
    const rule = Array.isArray(result.rules) ? result.rules[0] : result.rules;

    expect(rule).toEqual({ userAgent: '*', disallow: '/' });
    expect(result.sitemap).toBeUndefined();
  });
});

describe('proxy applies the headers to every response', () => {
  const request = (path: string) => new NextRequest(`http://localhost:3000${path}`);

  it('staging: noindex + security headers on shop pages, robots.txt, the sitemap and errors', () => {
    vi.stubEnv('SITE_ENV', 'staging');

    for (const path of ['/', '/product/x', '/about', '/robots.txt', '/sitemap.xml', '/media/a.jpg', '/no-such-page']) {
      const response = proxy(request(path));
      expect(response.headers.get('x-robots-tag'), path).toBe('noindex, nofollow');
      expect(response.headers.get('x-content-type-options'), path).toBe('nosniff');
      expect(response.headers.get('x-frame-options'), path).toBe('DENY');
      expect(response.headers.get('strict-transport-security'), path).toBeNull();
    }
  });

  it('production: indexable pages carry no noindex header, and get HSTS', () => {
    vi.stubEnv('SITE_ENV', 'production');

    for (const path of ['/', '/product/x', '/about']) {
      const response = proxy(request(path));
      expect(response.headers.get('x-robots-tag'), path).toBeNull();
      expect(response.headers.get('strict-transport-security'), path).toBe('max-age=31536000; includeSubDomains');
    }
  });

  it('the order page gets no-referrer, every other shop page strict-origin-when-cross-origin', () => {
    expect(proxy(request('/order/abc')).headers.get('referrer-policy')).toBe('no-referrer');
    expect(proxy(request('/cart')).headers.get('referrer-policy')).toBe('strict-origin-when-cross-origin');
  });

  it('the admin stays noindex even in production, and redirects (with the same headers) when signed out', () => {
    vi.stubEnv('SITE_ENV', 'production');

    const response = proxy(request('/admin/orders'));

    expect(response.status).toBe(307);
    expect(response.headers.get('x-robots-tag')).toBe('noindex, nofollow');
    expect(response.headers.get('cache-control')).toBe('no-store');
    expect(response.headers.get('referrer-policy')).toBe('no-referrer');
    expect(response.headers.get('x-frame-options')).toBe('DENY');
  });
});
