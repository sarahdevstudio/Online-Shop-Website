import { createCipheriv, createDecipheriv, createHash, randomBytes } from 'node:crypto';
import { cookies } from 'next/headers';
import { apiBaseUrl } from './api';
import { readAdminSession } from './admin/api';
import { readSessionConfig } from './admin/session';

/**
 * Draft preview of the site configuration (docs/10 §9.2 step 2).
 *
 * HOW THE ADMIN UI USES IT (for the admin fork)
 *  - START: call `startSitePreview()` from a Server Action that runs on an /admin page, then `redirect('/')`. It must run there
 *    because the admin session cookie only travels under /admin. It checks the admin session itself and sets the preview cookie.
 *    (A plain link or a route handler outside /admin cannot see the admin cookie, so there is deliberately no /api/.../start.)
 *  - STOP: a plain link to `/api/site-preview/stop` (optionally `?next=/admin/site`) clears the cookie and redirects; the banner
 *    shown on every previewed page links there. The admin may also call `stopSitePreview()` from a Server Action.
 *
 * HOW IT WORKS
 *  - Cookie `site_preview`: httpOnly, SameSite=Strict, Path=/, 30 minutes, sealed (AES-256-GCM) with a key derived from the same
 *    secret as `admin_session` but with its own label (a sealed admin session is not a valid preview cookie and vice versa).
 *    It carries a copy of the admin's API auth, the admin session's login time and its own expiry.
 *  - While valid, `getSiteConfig()` reads `GET /api/v1/admin/site/compiled?state=draft` as that admin with `no-store`. If the API
 *    refuses (401/403: session revoked, signed out, permission removed) the preview silently turns off.
 *  - Previewed responses get `noindex` and `Cache-Control: private, no-store` (proxy.ts, metadata) and a fixed banner.
 */

export const PREVIEW_COOKIE = 'site_preview';
export const PREVIEW_MINUTES = 30;

interface PreviewToken {
  v: 1;
  /** The API's `shop_auth` cookie value of the admin who started the preview. */
  auth: string;
  /** Login time of that admin session (ms): ties the cookie to one admin session. */
  iat: number;
  /** Expiry (ms since epoch). */
  exp: number;
}

function keyFrom(secret: string): Buffer {
  return createHash('sha256').update(`site-preview-v1\0${secret}`).digest();
}

export function sealPreview(token: PreviewToken, secret: string): string {
  const iv = randomBytes(12);
  const cipher = createCipheriv('aes-256-gcm', keyFrom(secret), iv);
  const encrypted = Buffer.concat([cipher.update(JSON.stringify(token), 'utf8'), cipher.final()]);
  return Buffer.concat([iv, cipher.getAuthTag(), encrypted]).toString('base64url');
}

/** The token, or null for anything not sealed with this secret (tampered, truncated, foreign, an admin-session cookie) or expired. */
export function unsealPreview(value: string | undefined, secret: string, now: number): PreviewToken | null {
  if (!value) return null;
  try {
    const raw = Buffer.from(value, 'base64url');
    if (raw.length < 12 + 16 + 2) return null;
    const decipher = createDecipheriv('aes-256-gcm', keyFrom(secret), raw.subarray(0, 12));
    decipher.setAuthTag(raw.subarray(12, 28));
    const json = Buffer.concat([decipher.update(raw.subarray(28)), decipher.final()]).toString('utf8');
    const token = JSON.parse(json) as Partial<PreviewToken>;
    if (token.v !== 1 || typeof token.auth !== 'string' || token.auth.length === 0 || !Number.isFinite(token.iat) || !Number.isFinite(token.exp)) return null;
    return (token.exp as number) > now ? (token as PreviewToken) : null;
  } catch {
    return null;
  }
}

export function previewCookieOptions(production: boolean) {
  return { httpOnly: true, secure: production, sameSite: 'strict' as const, path: '/', maxAge: PREVIEW_MINUTES * 60 };
}

/** Starts the preview for the signed-in admin. Only valid inside a Server Action or Route Handler under /admin. */
export async function startSitePreview(): Promise<boolean> {
  const session = await readAdminSession();
  if (!session) return false;
  const config = readSessionConfig();
  const now = Date.now();
  const token: PreviewToken = { v: 1, auth: session.auth, iat: session.iat, exp: now + PREVIEW_MINUTES * 60_000 };
  (await cookies()).set(PREVIEW_COOKIE, sealPreview(token, config.secret), previewCookieOptions(config.production));
  return true;
}

export async function stopSitePreview(): Promise<void> {
  const config = readSessionConfig();
  (await cookies()).set(PREVIEW_COOKIE, '', { ...previewCookieOptions(config.production), maxAge: 0 });
}

/** The auth to preview as, from a valid cookie; null when there is none (or outside a request). */
export async function readPreviewAuth(): Promise<string | null> {
  try {
    const jar = await cookies();
    const value = jar.get(PREVIEW_COOKIE)?.value;
    if (!value) return null;
    return unsealPreview(value, readSessionConfig().secret, Date.now())?.auth ?? null;
  } catch {
    return null;
  }
}

const TIMEOUT_MS = 5_000;

/** The raw compiled draft document as that admin, or null when the API refuses or fails (the preview turns itself off). */
export async function fetchDraftDocument(auth: string): Promise<unknown | null> {
  try {
    const response = await fetch(`${apiBaseUrl()}/api/v1/admin/site/compiled?state=draft`, {
      headers: { accept: 'application/json', cookie: `shop_auth=${auth}` },
      cache: 'no-store',
      signal: AbortSignal.timeout(TIMEOUT_MS),
    });
    if (!response.ok) return null;
    return ((await response.json()) as { result?: unknown }).result ?? null;
  } catch {
    return null;
  }
}
