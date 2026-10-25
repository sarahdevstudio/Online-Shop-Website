import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ADMIN_COOKIE, readSessionConfig, sealSession, type AdminSession } from '../src/lib/admin/session';

const cfg = readSessionConfig();
const session: AdminSession = { v: 1, auth: 'AUTH-VALUE', email: 'owner@shop.test', iat: Date.now() - 1000, seen: Date.now() - 1000 };

const jar = { value: undefined as string | undefined };
vi.mock('next/headers', () => ({
  cookies: async () => ({ get: (name: string) => (name === ADMIN_COOKIE && jar.value ? { value: jar.value } : undefined), set: vi.fn() }),
  headers: async () => new Headers({ 'x-forwarded-for': '203.0.113.9' }),
}));
vi.mock('next/navigation', () => ({
  redirect: (url: string) => {
    throw new Error(`REDIRECT:${url}`);
  },
}));
vi.mock('next/server', () => ({ connection: vi.fn(async () => undefined) }));

process.env.API_BASE_URL = 'http://api.test';
const { adminFetch, cookieValue } = await import('../src/lib/admin/api');

interface Call {
  url: string;
  method: string;
  headers: Record<string, string>;
  body?: string;
}
let calls: Call[] = [];

function respond(status: number, body: unknown, setCookies: string[] = []) {
  const headers = new Headers({ 'content-type': 'application/json' });
  const response = new Response(body === null ? null : JSON.stringify(body), { status, headers });
  vi.spyOn(response.headers, 'getSetCookie').mockReturnValue(setCookies);
  return response;
}

function mockApi(handler: (call: Call) => Response) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init: RequestInit) => {
      const call: Call = { url, method: init.method ?? 'GET', headers: Object.fromEntries(Object.entries((init.headers ?? {}) as Record<string, string>)), body: init.body as string | undefined };
      calls.push(call);
      return handler(call);
    }),
  );
}

beforeEach(() => {
  calls = [];
  jar.value = sealSession(session, cfg.secret);
  vi.unstubAllGlobals();
});

describe('adminFetch', () => {
  it('redirects to the login page when there is no valid session', async () => {
    jar.value = undefined;
    mockApi(() => respond(200, {}));

    await expect(adminFetch('GET', '/admin/dashboard')).rejects.toThrow('REDIRECT:/admin/login');
    expect(calls).toHaveLength(0); // nothing reaches the API without a session
  });

  it('GET: replays only the API auth cookie, forwards the trusted IP, and needs no antiforgery token', async () => {
    mockApi(() => respond(200, { newOrders: 1 }));

    const result = await adminFetch<{ newOrders: number }>('GET', '/admin/dashboard');

    expect(result.ok && result.body.newOrders).toBe(1);
    expect(calls).toHaveLength(1);
    expect(calls[0].url).toBe('http://api.test/api/v1/admin/dashboard');
    expect(calls[0].headers.cookie).toBe('shop_auth=AUTH-VALUE');
    expect(calls[0].headers['x-xsrf-token']).toBeUndefined();
    expect(calls[0].headers['x-storefront-key']).toBeUndefined(); // the admin API uses the session, not the storefront key
  });

  it('POST: fetches an identity-bound antiforgery token first and sends cookie + header with the write', async () => {
    mockApi((call) =>
      call.url.endsWith('/auth/csrf')
        ? respond(200, { headerName: 'X-XSRF-TOKEN', token: 'CSRF-TOKEN' }, ['shop_csrf=CSRF-COOKIE; path=/; httponly'])
        : respond(200, { ok: true }),
    );

    const result = await adminFetch('POST', '/admin/orders/5/cancel');

    expect(result.ok).toBe(true);
    expect(calls.map((c) => c.url)).toEqual(['http://api.test/api/v1/auth/csrf', 'http://api.test/api/v1/admin/orders/5/cancel']);
    expect(calls[0].headers.cookie).toBe('shop_auth=AUTH-VALUE'); // bound to the admin's identity
    expect(calls[1].headers.cookie).toBe('shop_auth=AUTH-VALUE; shop_csrf=CSRF-COOKIE');
    expect(calls[1].headers['x-xsrf-token']).toBe('CSRF-TOKEN');
  });

  it('JSON bodies are sent as JSON; the browser never supplies the API headers', async () => {
    mockApi((call) => (call.url.endsWith('/auth/csrf') ? respond(200, { headerName: 'X-XSRF-TOKEN', token: 't' }, ['shop_csrf=c']) : respond(200, {})));

    await adminFetch('PUT', '/admin/settings', { body: { settings: { shippingFlatFee: 1 } } });

    expect(calls[1].headers['content-type']).toBe('application/json');
    expect(JSON.parse(calls[1].body!)).toEqual({ settings: { shippingFlatFee: 1 } });
  });

  it('a 401 from the API (revoked session) sends the admin back to the login page', async () => {
    mockApi(() => respond(401, { code: 'unauthorized' }));

    await expect(adminFetch('GET', '/admin/orders')).rejects.toThrow('REDIRECT:/admin/login');
  });

  it('returns 409 / 403 / 400 problems to the caller (for the "data changed" banner and field errors)', async () => {
    mockApi((call) => (call.url.endsWith('/auth/csrf') ? respond(200, { headerName: 'X-XSRF-TOKEN', token: 't' }, ['shop_csrf=c']) : respond(409, { title: 'modified', status: 409 })));

    const conflict = await adminFetch('PUT', '/admin/variants/3', { body: {} });
    expect(conflict).toMatchObject({ ok: false, status: 409, problem: { code: null } });

    mockApi(() => respond(403, { code: 'step_up_failed', detail: 'x' }));
    expect(await adminFetch('GET', '/admin/settings')).toMatchObject({ ok: false, status: 403, problem: { code: 'step_up_failed' } });
  });

  it('an API outage is an error, not a silent empty page', async () => {
    mockApi(() => respond(503, { title: 'down' }));
    await expect(adminFetch('GET', '/admin/orders')).rejects.toThrow(/503/);
  });
});

describe('cookieValue', () => {
  it('reads one cookie out of Set-Cookie lines', () => {
    expect(cookieValue(['a=1; path=/', 'shop_auth=XYZ; httponly; samesite=lax'], 'shop_auth')).toBe('XYZ');
    expect(cookieValue(['a=1'], 'shop_auth')).toBeNull();
  });
});
