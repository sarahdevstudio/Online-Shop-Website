import { NextRequest } from 'next/server';
import { describe, expect, it } from 'vitest';
import { proxy, config } from '../src/proxy';
import { ADMIN_COOKIE, readSessionConfig, sealSession, unsealSession, type AdminSession } from '../src/lib/admin/session';

const cfg = readSessionConfig(); // development secret in tests
const now = Date.now();
const fresh: AdminSession = { v: 1, auth: 'api-cookie', email: 'owner@shop.test', iat: now - 60_000, seen: now - 60_000 };

function request(path: string, cookie?: string) {
  return new NextRequest(`http://localhost:3000${path}`, { headers: cookie ? { cookie: `${ADMIN_COOKIE}=${cookie}` } : {} });
}

describe('admin proxy', () => {
  it('sends a request without a session to the login page', () => {
    const response = proxy(request('/admin/orders'));

    expect(response.status).toBe(307);
    expect(response.headers.get('location')).toBe('http://localhost:3000/admin/login');
    expect(response.headers.get('x-robots-tag')).toContain('noindex');
  });

  it('refuses a forged or foreign cookie and deletes it', () => {
    const response = proxy(request('/admin', 'AAAA'));
    expect(response.headers.get('location')).toContain('/admin/login');
    expect(response.cookies.get(ADMIN_COOKIE)?.value).toBe('');

    const otherSecret = sealSession(fresh, 'another-secret-another-secret-another-secret');
    expect(proxy(request('/admin', otherSecret)).headers.get('location')).toContain('/admin/login');
  });

  it('sends idle- and absolute-expired sessions to the login page', () => {
    const idle = sealSession({ ...fresh, seen: now - 61 * 60_000 }, cfg.secret);
    const absolute = sealSession({ ...fresh, iat: now - 9 * 3_600_000, seen: now - 1000 }, cfg.secret);

    expect(proxy(request('/admin/products', idle)).headers.get('location')).toContain('/admin/login');
    expect(proxy(request('/admin/products', absolute)).headers.get('location')).toContain('/admin/login');
  });

  it('lets a valid session through, renews its idle timer and keeps the login time', () => {
    const response = proxy(request('/admin/orders', sealSession(fresh, cfg.secret)));

    expect(response.status).toBe(200);
    const renewed = unsealSession(response.cookies.get(ADMIN_COOKIE)!.value, cfg.secret)!;
    expect(renewed.seen).toBeGreaterThan(fresh.seen);
    expect(renewed.iat).toBe(fresh.iat);
    expect(renewed.auth).toBe('api-cookie');
    const cookie = response.cookies.get(ADMIN_COOKIE)!;
    expect(cookie).toMatchObject({ path: '/admin', httpOnly: true, sameSite: 'strict' });
    expect(response.headers.get('cache-control')).toBe('no-store');
    expect(response.headers.get('x-robots-tag')).toBe('noindex, nofollow');
  });

  it('serves the login page without a session', () => {
    const response = proxy(request('/admin/login'));
    expect(response.status).toBe(200);
    expect(response.headers.get('x-robots-tag')).toContain('noindex');
  });

  it('covers every page and route handler except static files and the streaming upload route', () => {
    const pattern = new RegExp(`^${config.matcher[0]}$`);
    for (const path of ['/', '/admin', '/admin/orders/5', '/order/abc', '/cart', '/robots.txt', '/sitemap.xml', '/media/a.jpg', '/api/anything']) {
      expect(pattern.test(path), path).toBe(true);
    }
    for (const path of ['/_next/static/chunks/a.js', '/_next/image', '/admin/api/products/5/images']) {
      expect(pattern.test(path), path).toBe(false);
    }
  });
});
