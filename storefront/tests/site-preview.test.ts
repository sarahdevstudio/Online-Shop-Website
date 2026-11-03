import { describe, expect, it } from 'vitest';
import { sealSession, unsealSession } from '../src/lib/admin/session';
import { PREVIEW_MINUTES, previewCookieOptions, sealPreview, unsealPreview } from '../src/lib/site-preview';

const SECRET = 'a-long-random-secret-for-tests-0123456789abcdef';
const NOW = 1_700_000_000_000;
const token = { v: 1 as const, auth: 'api-auth-cookie-value', iat: NOW - 1000, exp: NOW + PREVIEW_MINUTES * 60_000 };

describe('preview cookie', () => {
  it('round-trips while valid', () => {
    expect(unsealPreview(sealPreview(token, SECRET), SECRET, NOW)).toEqual(token);
  });

  it('expires', () => {
    expect(unsealPreview(sealPreview(token, SECRET), SECRET, token.exp)).toBeNull();
    expect(unsealPreview(sealPreview(token, SECRET), SECRET, token.exp + 1)).toBeNull();
  });

  it('rejects another secret, tampering, truncation and garbage', () => {
    const sealed = sealPreview(token, SECRET);
    expect(unsealPreview(sealed, 'another-secret-another-secret-another-0000', NOW)).toBeNull();
    expect(unsealPreview(`${sealed.slice(0, -2)}AA`, SECRET, NOW)).toBeNull();
    expect(unsealPreview(sealed.slice(0, 20), SECRET, NOW)).toBeNull();
    expect(unsealPreview('not a cookie', SECRET, NOW)).toBeNull();
    expect(unsealPreview(undefined, SECRET, NOW)).toBeNull();
  });

  it('is not interchangeable with the admin session cookie', () => {
    const adminCookie = sealSession({ v: 1, auth: 'x', username: 'u', iat: NOW, seen: NOW }, SECRET);
    expect(unsealPreview(adminCookie, SECRET, NOW)).toBeNull();
    expect(unsealSession(sealPreview(token, SECRET), SECRET)).toBeNull();
  });

  it('is httpOnly, SameSite=Strict, Path=/ and lasts 30 minutes', () => {
    expect(previewCookieOptions(true)).toEqual({ httpOnly: true, secure: true, sameSite: 'strict', path: '/', maxAge: 1800 });
    expect(previewCookieOptions(false).secure).toBe(false);
  });
});
