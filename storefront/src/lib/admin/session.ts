import { createCipheriv, createDecipheriv, createHash, randomBytes } from 'node:crypto';

/**
 * The admin session. The browser holds only `admin_session`: the API's own auth cookie value, sealed (AES-256-GCM) with a
 * secret only the Next server knows, plus the times needed for the idle and absolute limits. No server-side session table.
 */

export const ADMIN_COOKIE = 'admin_session';

export interface AdminSession {
  v: 1;
  /** The API's `shop_auth` cookie value. Never sent to the browser in clear text. */
  auth: string;
  username: string;
  /** Login time (ms since epoch): the absolute limit counts from here. */
  iat: number;
  /** Last activity (ms): the idle limit counts from here. */
  seen: number;
}

export interface SessionLimits {
  idleMs: number;
  absoluteMs: number;
}

export const DEFAULT_IDLE_MINUTES = 60;
export const DEFAULT_ABSOLUTE_HOURS = 8;
const DEV_SECRET = 'dev-admin-session-secret-not-for-production-0123456789';

function keyFrom(secret: string): Buffer {
  return createHash('sha256').update(`admin-session-v1\0${secret}`).digest();
}

export function sealSession(session: AdminSession, secret: string): string {
  const iv = randomBytes(12);
  const cipher = createCipheriv('aes-256-gcm', keyFrom(secret), iv);
  const encrypted = Buffer.concat([cipher.update(JSON.stringify(session), 'utf8'), cipher.final()]);
  return Buffer.concat([iv, cipher.getAuthTag(), encrypted]).toString('base64url');
}

/** The session, or null for anything that was not sealed with this secret (tampered, truncated, foreign, garbage). */
export function unsealSession(token: string | undefined, secret: string): AdminSession | null {
  if (!token) return null;
  try {
    const raw = Buffer.from(token, 'base64url');
    if (raw.length < 12 + 16 + 2) return null;
    const decipher = createDecipheriv('aes-256-gcm', keyFrom(secret), raw.subarray(0, 12));
    decipher.setAuthTag(raw.subarray(12, 28));
    const json = Buffer.concat([decipher.update(raw.subarray(28)), decipher.final()]).toString('utf8');
    const value = JSON.parse(json) as Partial<AdminSession>;
    if (
      value.v !== 1 ||
      typeof value.auth !== 'string' ||
      value.auth.length === 0 ||
      typeof value.username !== 'string' ||
      !Number.isFinite(value.iat) ||
      !Number.isFinite(value.seen)
    ) {
      return null;
    }
    return value as AdminSession;
  } catch {
    return null;
  }
}

export type SessionState = 'ok' | 'idle' | 'absolute';

export function sessionState(session: AdminSession, now: number, limits: SessionLimits): SessionState {
  if (now - session.iat >= limits.absoluteMs) return 'absolute';
  if (now - session.seen >= limits.idleMs) return 'idle';
  return 'ok';
}

export function touchSession(session: AdminSession, now: number): AdminSession {
  return { ...session, seen: now };
}

/** Why a sealing secret is unacceptable, or null when it is fine. Production refuses to start otherwise. */
export function secretProblem(secret: string | undefined): string | null {
  if (!secret) return 'ADMIN_SESSION_SECRET is not set.';
  if (Buffer.byteLength(secret, 'utf8') < 32) return 'ADMIN_SESSION_SECRET must be at least 32 bytes.';
  const lower = secret.toLowerCase();
  const placeholder =
    lower.startsWith('dev-') ||
    lower.startsWith('test-') ||
    ['placeholder', 'change-me', 'changeme', 'change_me', 'your-', 'your_', 'example', 'replace'].some((word) => lower.includes(word)) ||
    new Set(secret).size < 8;
  return placeholder ? 'ADMIN_SESSION_SECRET looks like a placeholder; use random bytes.' : null;
}

function boundedNumber(value: string | undefined, fallback: number, min: number, max: number): number {
  const n = Number.parseFloat(value ?? '');
  return Number.isFinite(n) && n >= min && n <= max ? n : fallback;
}

export interface SessionConfig {
  secret: string;
  limits: SessionLimits;
  production: boolean;
}

/** Environment: ADMIN_SESSION_SECRET, ADMIN_SESSION_IDLE_MINUTES (default 60), ADMIN_SESSION_ABSOLUTE_HOURS (default 8). */
export function readSessionConfig(env: Record<string, string | undefined> = process.env): SessionConfig {
  const production = env.NODE_ENV === 'production';
  let secret = env.ADMIN_SESSION_SECRET;
  if (!secret && !production) secret = DEV_SECRET;
  if (production) {
    const problem = secretProblem(secret);
    if (problem) throw new Error(problem);
  }
  return {
    secret: secret as string,
    production,
    limits: {
      idleMs: boundedNumber(env.ADMIN_SESSION_IDLE_MINUTES, DEFAULT_IDLE_MINUTES, 1, 24 * 60) * 60_000,
      absoluteMs: boundedNumber(env.ADMIN_SESSION_ABSOLUTE_HOURS, DEFAULT_ABSOLUTE_HOURS, 1, 72) * 3_600_000,
    },
  };
}

/** `httpOnly`, `Secure` (production), `SameSite=Strict`, `Path=/admin`, gone from the browser after the absolute limit. */
export function sessionCookieOptions(config: Pick<SessionConfig, 'production' | 'limits'>) {
  return {
    httpOnly: true,
    secure: config.production,
    sameSite: 'strict' as const,
    path: '/admin',
    maxAge: Math.floor(config.limits.absoluteMs / 1000),
  };
}
