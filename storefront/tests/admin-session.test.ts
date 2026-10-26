import { afterEach, describe, expect, it, vi } from 'vitest';
import robots from '../src/app/robots';
import { requestTooLarge, uploadErrorMessage, MAX_IMAGE_BYTES } from '../src/lib/admin/image-upload';
import { LoginLimiter } from '../src/lib/admin/login-limiter';
import {
  readSessionConfig,
  sealSession,
  secretProblem,
  sessionCookieOptions,
  sessionState,
  touchSession,
  unsealSession,
  type AdminSession,
} from '../src/lib/admin/session';
import { camelKeys, isConflict, problemState } from '../src/lib/admin/form-state';
import nextConfig from '../next.config';

const SECRET = 'k7Qx9mZp2VbN4tLw8RsY1uHd6GfJ3cAe-0123';
const NOW = 1_800_000_000_000;
const session: AdminSession = { v: 1, auth: 'api-cookie-value', username: 'owner', iat: NOW, seen: NOW };
const LIMITS = { idleMs: 60 * 60_000, absoluteMs: 8 * 3_600_000 };

describe('sealed admin session', () => {
  it('round-trips and never shows the API cookie in clear', () => {
    const token = sealSession(session, SECRET);

    expect(unsealSession(token, SECRET)).toEqual(session);
    expect(token).not.toContain('api-cookie-value');
    expect(Buffer.from(token, 'base64url').toString('utf8')).not.toContain('api-cookie-value');
  });

  it('rejects tampering, a wrong secret, truncation and garbage', () => {
    const token = sealSession(session, SECRET);
    const raw = Buffer.from(token, 'base64url');
    const flipped = Buffer.from(raw);
    flipped[flipped.length - 1] ^= 1;

    expect(unsealSession(flipped.toString('base64url'), SECRET)).toBeNull();
    expect(unsealSession(token, SECRET + 'x')).toBeNull();
    expect(unsealSession(raw.subarray(0, 20).toString('base64url'), SECRET)).toBeNull();
    expect(unsealSession('not a token', SECRET)).toBeNull();
    expect(unsealSession('', SECRET)).toBeNull();
    expect(unsealSession(undefined, SECRET)).toBeNull();
  });

  it('uses a fresh nonce each time', () => {
    expect(sealSession(session, SECRET)).not.toBe(sealSession(session, SECRET));
  });

  it('rejects a well-formed token whose content is not a session', () => {
    const forged = sealSession({ ...session, auth: '' } as AdminSession, SECRET);
    expect(unsealSession(forged, SECRET)).toBeNull();
  });
});

describe('session lifetime', () => {
  it('is ok inside the idle and absolute limits', () => {
    expect(sessionState(session, NOW + 59 * 60_000, LIMITS)).toBe('ok');
  });

  it('expires after 60 minutes without activity', () => {
    expect(sessionState(session, NOW + 60 * 60_000, LIMITS)).toBe('idle');
  });

  it('activity renews the idle timer but never the absolute limit', () => {
    let current = session;
    for (let minute = 30; minute <= 450; minute += 30) {
      expect(sessionState(current, NOW + minute * 60_000, LIMITS)).toBe('ok');
      current = touchSession(current, NOW + minute * 60_000);
    }
    expect(sessionState(current, NOW + 8 * 3_600_000, LIMITS)).toBe('absolute'); // 8 h after login, however active
  });

  it('is configurable and bounded', () => {
    const custom = readSessionConfig({ NODE_ENV: 'test', ADMIN_SESSION_IDLE_MINUTES: '15', ADMIN_SESSION_ABSOLUTE_HOURS: '2' });
    expect(custom.limits).toEqual({ idleMs: 15 * 60_000, absoluteMs: 2 * 3_600_000 });
    const defaults = readSessionConfig({ NODE_ENV: 'test', ADMIN_SESSION_IDLE_MINUTES: 'abc', ADMIN_SESSION_ABSOLUTE_HOURS: '9999' });
    expect(defaults.limits).toEqual({ idleMs: 60 * 60_000, absoluteMs: 8 * 3_600_000 });
  });
});

describe('sealing secret', () => {
  it('production refuses a missing, short or placeholder secret', () => {
    for (const bad of [undefined, '', 'short', 'dev-admin-session-secret-not-for-production-0123456789', 'change-me-change-me-change-me-123456', 'a'.repeat(40)]) {
      expect(() => readSessionConfig({ NODE_ENV: 'production', ADMIN_SESSION_SECRET: bad }), String(bad)).toThrow();
    }
    expect(secretProblem(undefined)).toContain('not set');
    expect(secretProblem('short')).toContain('32 bytes');
  });

  it('production accepts a long random secret; development falls back to a development one', () => {
    expect(readSessionConfig({ NODE_ENV: 'production', ADMIN_SESSION_SECRET: SECRET }).secret).toBe(SECRET);
    expect(readSessionConfig({ NODE_ENV: 'development' }).secret.length).toBeGreaterThanOrEqual(32);
  });
});

describe('admin_session cookie', () => {
  it('is httpOnly, SameSite=Strict, Path=/admin, and Secure in production', () => {
    const prod = sessionCookieOptions({ production: true, limits: LIMITS });
    expect(prod).toMatchObject({ httpOnly: true, secure: true, sameSite: 'strict', path: '/admin', maxAge: 8 * 3600 });
    expect(sessionCookieOptions({ production: false, limits: LIMITS }).secure).toBe(false);
  });
});

describe('login limiter', () => {
  it('allows N attempts per window per key, independently per key', () => {
    let now = 0;
    const limiter = new LoginLimiter(3, 60_000, () => now);

    expect([1, 2, 3, 4].map(() => limiter.tryAcquire('1.1.1.1|a@x'))).toEqual([true, true, true, false]);
    expect(limiter.tryAcquire('1.1.1.1|b@x')).toBe(true); // another account
    now = 59_000;
    expect(limiter.tryAcquire('1.1.1.1|a@x')).toBe(false);
    now = 61_000;
    expect(limiter.tryAcquire('1.1.1.1|a@x')).toBe(true); // window passed
  });
});

describe('image upload limits', () => {
  it('accepts up to 5 MB plus multipart overhead and refuses bigger requests', () => {
    expect(requestTooLarge(null)).toBe(false);
    expect(requestTooLarge(4_900_000)).toBe(false);
    expect(requestTooLarge(MAX_IMAGE_BYTES + 400)).toBe(false); // a 5 MB file with its multipart envelope
    expect(requestTooLarge(MAX_IMAGE_BYTES + 100_000)).toBe(true);
  });

  it('turns API rejections into clear Persian messages', () => {
    expect(uploadErrorMessage(413, null)).toContain('۵ مگابایت');
    expect(uploadErrorMessage(400, { file: ['حجم تصویر باید بین ۱ بایت و 5242880 بایت باشد.'] })).toContain('۵ مگابایت');
    expect(uploadErrorMessage(400, { file: ['فقط تصویرهای JPEG، PNG و WebP پذیرفته می‌شوند.'] })).toContain('JPEG');
    expect(uploadErrorMessage(500, null)).toContain('بارگذاری تصویر انجام نشد');
  });
});

describe('form state mapping', () => {
  it('camel-cases API validation keys', () => {
    expect(camelKeys({ FullName: ['الف'], 'Variants[0].Sku': ['ب'], empty: [] })).toEqual({ fullName: 'الف', 'variants[0].sku': 'ب' });
  });

  it('treats a 409 without a business code as "data changed", and keeps what was typed', () => {
    expect(isConflict({ status: 409, code: null, detail: null, errors: null })).toBe(true);
    expect(isConflict({ status: 409, code: 'concurrent_update', detail: null, errors: null })).toBe(true);
    expect(isConflict({ status: 409, code: 'sku_taken', detail: 'x', errors: null })).toBe(false);

    const state = problemState({ status: 409, code: null, detail: null, errors: null }, { sku: 'A-1', price: '5' });
    expect(state.conflict).toBe(true);
    expect(state.submitted).toEqual({ sku: 'A-1', price: '5' });
    expect(state.message).toContain('هم‌زمان');
  });

  it('maps step-up failures, validation and business conflicts', () => {
    expect(problemState({ status: 403, code: 'step_up_failed', detail: null, errors: null }).message).toContain('رمز عبور فعلی');
    const invalid = problemState({ status: 400, code: 'validation_failed', detail: null, errors: { CardNumber: ['شماره کارت معتبر نیست.'] } });
    expect(invalid.errors).toEqual({ cardNumber: 'شماره کارت معتبر نیست.' });
    const taken = problemState({ status: 409, code: 'sku_taken', detail: 'این کد کالا قبلاً استفاده شده است.', errors: null });
    expect(taken.conflict).toBe(false);
    expect(taken.message).toBe('این کد کالا قبلاً استفاده شده است.');
  });
});

describe('admin is never indexed', () => {
  afterEach(() => vi.unstubAllEnvs());

  it('robots.txt (production) disallows /admin', () => {
    vi.stubEnv('SITE_ENV', 'production');
    const rules = robots().rules;
    const disallow = ([] as string[]).concat(...(Array.isArray(rules) ? rules : [rules]).map((r) => r.disallow ?? []));
    expect(disallow).toContain('/admin');
  });

  it('every /admin response is noindex, no-store, unframed and sends no referrer', async () => {
    const headers = await nextConfig.headers!();
    const admin = headers.find((h) => h.source === '/admin/:path*')!;
    const map = Object.fromEntries(admin.headers.map((h) => [h.key, h.value]));
    expect(map['X-Robots-Tag']).toContain('noindex');
    expect(map['Cache-Control']).toBe('no-store');
    expect(map['Referrer-Policy']).toBe('no-referrer');
    expect(map['X-Frame-Options']).toBe('DENY');
    expect(headers.find((h) => h.source === '/admin')?.headers.some((h) => h.key === 'X-Robots-Tag')).toBe(true);
  });
});
