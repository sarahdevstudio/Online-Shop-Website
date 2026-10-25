import { describe, expect, it } from 'vitest';
import robots from '../src/app/robots';
import { CART_COOKIE, CART_MAX_AGE_SECONDS, cartCookieOptions } from '../src/lib/cart-cookie';
import { cartErrorKey, parseQuantity, parseVariantId } from '../src/lib/cart-input';
import { isIdempotencyKey, mapApiErrors, readCheckoutValues } from '../src/lib/checkout-form';
import { resolveClientIp, trustedProxyHops } from '../src/lib/client-ip';
import { maskTokens } from '../src/lib/log';
import nextConfig from '../next.config';

const TOKEN = 'AbCdEfGhIjKlMnOpQrStUvWxYz0123456789_-abcde'; // 43 characters

describe('resolveClientIp', () => {
  it('uses the entry the trusted proxy appended, never what the browser put on the left', () => {
    // browser sent "6.6.6.6, 7.7.7.7"; the single trusted proxy appended the real peer
    expect(resolveClientIp('6.6.6.6, 7.7.7.7, 203.0.113.9', 1, false)).toBe('203.0.113.9');
    expect(resolveClientIp('203.0.113.9', 1, false)).toBe('203.0.113.9');
  });

  it('counts hops from the right when several proxies are trusted', () => {
    // browser spoof, then CDN appended the client, then nginx appended the CDN
    expect(resolveClientIp('6.6.6.6, 203.0.113.9, 198.51.100.1', 2, false)).toBe('203.0.113.9');
    expect(resolveClientIp('198.51.100.1', 2, false)).toBeNull(); // chain shorter than the trusted hops
  });

  it('ignores the header without a trusted proxy, except in development with a single address', () => {
    expect(resolveClientIp('1.2.3.4', 0, false)).toBeNull();
    expect(resolveClientIp('1.2.3.4, 5.6.7.8', 0, true)).toBeNull(); // a chain can only be a spoof
    expect(resolveClientIp('::1', 0, true)).toBe('::1');
    expect(resolveClientIp('1.2.3.4', 0, true)).toBe('1.2.3.4');
  });

  it('rejects garbage and missing headers', () => {
    expect(resolveClientIp(null, 1, false)).toBeNull();
    expect(resolveClientIp('', 1, false)).toBeNull();
    expect(resolveClientIp('6.6.6.6, not-an-ip', 1, false)).toBeNull();
    expect(resolveClientIp("1.2.3.4'; drop table", 1, false)).toBeNull();
  });

  it('parses TRUSTED_PROXY_HOPS defensively', () => {
    expect(trustedProxyHops('1')).toBe(1);
    expect(trustedProxyHops('2')).toBe(2);
    expect(trustedProxyHops(undefined)).toBe(0);
    expect(trustedProxyHops('')).toBe(0);
    expect(trustedProxyHops('-1')).toBe(0);
    expect(trustedProxyHops('abc')).toBe(0);
    expect(trustedProxyHops('99')).toBe(0);
  });
});

describe('maskTokens', () => {
  it('hides order tokens in paths and messages', () => {
    expect(maskTokens(`/order/${TOKEN}`)).toBe('/order/***');
    expect(maskTokens(`POST /orders/${TOKEN}/transfer-reference`)).toBe('POST /orders/***/transfer-reference');
    expect(maskTokens(`GET /orders/${TOKEN}`)).not.toContain(TOKEN);
  });

  it('leaves other paths alone', () => {
    expect(maskTokens('/cart')).toBe('/cart');
    expect(maskTokens('/orders/12')).toBe('/orders/12');
    expect(maskTokens('/product/led-bulb')).toBe('/product/led-bulb');
  });
});

describe('cart cookie', () => {
  it('is httpOnly, SameSite=Lax, site-wide and lasts 30 days', () => {
    const options = cartCookieOptions(true);
    expect(CART_COOKIE).toBe('cart');
    expect(options).toMatchObject({ httpOnly: true, secure: true, sameSite: 'lax', path: '/', maxAge: 30 * 24 * 60 * 60 });
    expect(CART_MAX_AGE_SECONDS).toBe(2_592_000);
  });

  it('is Secure in production and only not Secure in development', () => {
    expect(cartCookieOptions(true).secure).toBe(true);
    expect(cartCookieOptions(false).secure).toBe(false);
  });
});

describe('form input parsing', () => {
  it('accepts Persian digits and rejects nonsense', () => {
    expect(parseVariantId('۱۲')).toBe(12);
    expect(parseVariantId('0')).toBeNull();
    expect(parseVariantId('-3')).toBeNull();
    expect(parseVariantId('abc')).toBeNull();
    expect(parseVariantId(null)).toBeNull();
    expect(parseQuantity('۳')).toBe(3);
    expect(parseQuantity('0')).toBeNull();
    expect(parseQuantity('0', true)).toBe(0);
    expect(parseQuantity('1001')).toBeNull();
    expect(parseQuantity('')).toBeNull();
  });

  it('maps API problems to message keys', () => {
    expect(cartErrorKey(429, null)).toBe('rate_limited');
    expect(cartErrorKey(409, 'variant_unavailable')).toBe('variant_unavailable');
    expect(cartErrorKey(400, null)).toBe('invalid');
  });

  it('reads checkout values back as strings and maps API field errors to camelCase', () => {
    const form = new FormData();
    form.set('fullName', 'علی');
    form.set('mobile', '۰۹۱۲۳۴۵۶۷۸۹');
    const values = readCheckoutValues(form);
    expect(values.fullName).toBe('علی');
    expect(values.mobile).toBe('۰۹۱۲۳۴۵۶۷۸۹'); // shown again exactly as typed
    expect(values.address).toBe('');
    expect(mapApiErrors({ FullName: ['a', 'b'], PostalCode: ['x'], Empty: [] })).toEqual({ fullName: 'a', postalCode: 'x' });
    expect(mapApiErrors(null)).toEqual({});
  });

  it('only accepts a GUID as the idempotency key', () => {
    expect(isIdempotencyKey('3f2504e0-4f89-11d3-9a0c-0305e82c3301')).toBe(true);
    expect(isIdempotencyKey('not-a-guid')).toBe(false);
    expect(isIdempotencyKey(null)).toBe(false);
  });
});

describe('indexing rules', () => {
  it('robots.txt disallows the cart, checkout and order pages', () => {
    const rules = robots().rules;
    const disallow = ([] as string[]).concat(...(Array.isArray(rules) ? rules : [rules]).map((r) => r.disallow ?? []));
    expect(disallow).toEqual(expect.arrayContaining(['/cart', '/checkout', '/order']));
  });

  it('the dev server does not log order URLs (they contain the access token)', () => {
    const logging = nextConfig.logging;
    const ignore = logging && logging.incomingRequests && typeof logging.incomingRequests === 'object' ? logging.incomingRequests.ignore : [];
    expect((ignore ?? []).some((pattern) => pattern.test(`/order/${TOKEN}`))).toBe(true);
    expect((ignore ?? []).some((pattern) => pattern.test('/cart'))).toBe(false);
  });

  it('the order page is sent noindex, no-store and with no referrer', async () => {
    const headers = await nextConfig.headers!();
    const order = headers.find((h) => h.source === '/order/:path*')!;
    const map = Object.fromEntries(order.headers.map((h) => [h.key, h.value]));
    expect(map['Referrer-Policy']).toBe('no-referrer');
    expect(map['Cache-Control']).toBe('no-store');
    expect(map['X-Robots-Tag']).toContain('noindex');
    for (const source of ['/cart', '/checkout']) {
      expect(headers.find((h) => h.source === source)?.headers.some((h) => h.key === 'X-Robots-Tag')).toBe(true);
    }
  });
});
