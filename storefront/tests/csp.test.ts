import { describe, expect, it } from 'vitest';
import { contentSecurityPolicy, newNonce, PERMISSIONS_POLICY } from '../src/lib/csp';

const directive = (policy: string, name: string) => policy.split('; ').find((d) => d.startsWith(`${name} `))?.slice(name.length + 1).split(' ');

describe('content security policy', () => {
  const policy = contentSecurityPolicy({ nonce: 'abc123' });

  it('production scripts: self, the request nonce and strict-dynamic — never eval or inline', () => {
    expect(directive(policy, 'script-src')).toEqual(["'self'", "'nonce-abc123'", "'strict-dynamic'"]);
    expect(policy).not.toContain('unsafe-eval');
    expect(directive(policy, 'script-src')).not.toContain("'unsafe-inline'");
  });

  it('only the development server may use eval and websockets', () => {
    const dev = contentSecurityPolicy({ nonce: 'n', development: true });
    expect(directive(dev, 'script-src')).toContain("'unsafe-eval'");
    expect(directive(dev, 'connect-src')).toContain('ws:');
    expect(directive(policy, 'connect-src')).toEqual(["'self'"]);
  });

  it('images, fonts and connections stay on the own origin; no plugins, no framing, no foreign forms', () => {
    expect(directive(policy, 'font-src')).toEqual(["'self'"]);
    expect(directive(policy, 'img-src')).toEqual(["'self'", 'data:', 'blob:']);
    expect(directive(policy, 'default-src')).toEqual(["'self'"]);
    expect(directive(policy, 'object-src')).toEqual(["'none'"]);
    expect(directive(policy, 'frame-ancestors')).toEqual(["'none'"]);
    expect(directive(policy, 'form-action')).toEqual(["'self'"]);
    expect(directive(policy, 'base-uri')).toEqual(["'self'"]);
  });

  it('style elements need the nonce; inline is allowed for style="" attributes only', () => {
    expect(directive(policy, 'style-src')).toEqual(["'self'", "'nonce-abc123'"]);
    expect(directive(policy, 'style-src-attr')).toEqual(["'unsafe-inline'"]);
  });

  it('a configured third party is added by name, to the directives asked for', () => {
    const withGa = contentSecurityPolicy({ nonce: 'n', extra: { script: ['https://www.googletagmanager.com'], connect: ['https://www.google-analytics.com'] } });
    expect(directive(withGa, 'script-src')).toContain('https://www.googletagmanager.com');
    expect(directive(withGa, 'connect-src')).toContain('https://www.google-analytics.com');
    expect(directive(withGa, 'img-src')).toEqual(["'self'", 'data:', 'blob:']);
  });

  it('nonces are unique, base64 and long enough', () => {
    const nonces = new Set(Array.from({ length: 50 }, newNonce));
    expect(nonces.size).toBe(50);
    for (const nonce of nonces) expect(nonce).toMatch(/^[A-Za-z0-9+/]{22}==$/);
  });

  it('permissions policy switches off the unused browser features', () => {
    for (const feature of ['camera', 'microphone', 'geolocation', 'payment', 'usb']) expect(PERMISSIONS_POLICY).toContain(`${feature}=()`);
  });
});
