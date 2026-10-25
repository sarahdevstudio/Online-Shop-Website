import { cookies } from 'next/headers';
import { redirect } from 'next/navigation';
import { ApiUnavailableError, apiBaseUrl } from '../api';
import { clientIpForApi } from '../store-api';
import {
  ADMIN_COOKIE,
  readSessionConfig,
  sealSession,
  sessionCookieOptions,
  sessionState,
  touchSession,
  unsealSession,
  type AdminSession,
} from './session';

/**
 * Server-side access to `/api/v1/admin/*` and `/api/v1/auth/*` for the admin panel. The browser never talks to the API and
 * never sees its cookies: this module unseals `admin_session`, replays the API's auth cookie, and does the antiforgery dance
 * (fetch a token bound to the admin's identity, send cookie + header) for every write, so the API's CSRF check is unchanged.
 */

export interface AdminProblem {
  status: number;
  code: string | null;
  detail: string | null;
  errors: Record<string, string[]> | null;
}

export type AdminResult<T> = { ok: true; status: number; body: T; setCookies: string[] } | { ok: false; status: number; problem: AdminProblem };

const TIMEOUT_MS = 15_000;

/** Value of one cookie out of `Set-Cookie` header lines. */
export function cookieValue(setCookies: readonly string[], name: string): string | null {
  for (const line of setCookies) {
    if (line.startsWith(`${name}=`)) return line.slice(name.length + 1).split(';')[0];
  }
  return null;
}

/** The current valid session, or null (no cookie, forged, idle- or absolute-expired). */
export async function readAdminSession(): Promise<AdminSession | null> {
  const jar = await cookies(); // first: reading request data makes the page dynamic, so nothing admin is prerendered at build
  const config = readSessionConfig();
  const session = unsealSession(jar.get(ADMIN_COOKIE)?.value, config.secret);
  return session && sessionState(session, Date.now(), config.limits) === 'ok' ? session : null;
}

/** Every admin page, action and route handler starts here; there is no way to forget it behind a shared proxy. */
export async function requireAdmin(): Promise<AdminSession> {
  const session = await readAdminSession();
  if (!session) redirect('/admin/login');
  return session;
}

/** Writes (or renews) the sealed cookie. Only valid inside a Server Action or Route Handler. */
export async function writeSessionCookie(session: AdminSession) {
  const config = readSessionConfig();
  (await cookies()).set(ADMIN_COOKIE, sealSession(session, config.secret), sessionCookieOptions(config));
}

export async function clearSessionCookie() {
  (await cookies()).set(ADMIN_COOKIE, '', { ...sessionCookieOptions(readSessionConfig()), maxAge: 0 });
}

function toProblem(status: number, json: unknown): AdminProblem {
  const raw = (json ?? {}) as Record<string, unknown>;
  return {
    status,
    code: typeof raw.code === 'string' ? raw.code : null,
    detail: typeof raw.detail === 'string' ? raw.detail : typeof raw.title === 'string' ? raw.title : null,
    errors: (raw.errors as Record<string, string[]> | undefined) ?? null,
  };
}

async function baseHeaders(): Promise<Record<string, string>> {
  const headers: Record<string, string> = { accept: 'application/json' };
  const ip = await clientIpForApi();
  if (ip) headers['x-forwarded-for'] = ip; // one address from the trusted proxy, never the browser's own header
  return headers;
}

/** `GET /auth/csrf` with the given cookies: the antiforgery cookie and the matching request token. */
export async function fetchCsrf(authCookie?: string): Promise<{ cookie: string; token: string; headerName: string }> {
  const headers = await baseHeaders();
  if (authCookie) headers.cookie = `shop_auth=${authCookie}`;
  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl()}/api/v1/auth/csrf`, { headers, cache: 'no-store', signal: AbortSignal.timeout(TIMEOUT_MS) });
  } catch (error) {
    throw new ApiUnavailableError('GET /auth/csrf failed', { cause: error });
  }
  if (!response.ok) throw new ApiUnavailableError(`GET /auth/csrf returned ${response.status}`);
  const body = (await response.json()) as { headerName: string; token: string };
  const cookie = cookieValue(response.headers.getSetCookie(), 'shop_csrf');
  if (!cookie) throw new ApiUnavailableError('GET /auth/csrf set no antiforgery cookie');
  return { cookie, token: body.token, headerName: body.headerName };
}

interface AdminRequest {
  body?: unknown;
  /** A raw body to stream through unchanged (image upload); `contentType` carries the multipart boundary. */
  stream?: { body: ReadableStream<Uint8Array>; contentType: string; contentLength?: string | null };
  timeoutMs?: number;
}

export async function adminFetch<T>(method: string, path: string, request: AdminRequest = {}): Promise<AdminResult<T>> {
  const session = await requireAdmin();
  const headers = await baseHeaders();
  let cookie = `shop_auth=${session.auth}`;

  if (method !== 'GET') {
    const csrf = await fetchCsrf(session.auth);
    cookie += `; shop_csrf=${csrf.cookie}`;
    headers[csrf.headerName.toLowerCase()] = csrf.token;
  }
  headers.cookie = cookie;

  let body: BodyInit | undefined;
  const init: RequestInit & { duplex?: 'half' } = { method, headers, cache: 'no-store', signal: AbortSignal.timeout(request.timeoutMs ?? TIMEOUT_MS) };
  if (request.stream) {
    headers['content-type'] = request.stream.contentType;
    if (request.stream.contentLength) headers['content-length'] = request.stream.contentLength;
    init.body = request.stream.body;
    init.duplex = 'half'; // streams the upload to the API without buffering it in memory
  } else if (request.body !== undefined) {
    headers['content-type'] = 'application/json';
    body = JSON.stringify(request.body);
    init.body = body;
  }

  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl()}/api/v1${path}`, init);
  } catch (error) {
    throw new ApiUnavailableError(`${method} ${path} failed`, { cause: error });
  }

  if (response.status === 401) redirect('/admin/login'); // revoked, expired or logged out elsewhere
  if (response.status >= 500) throw new ApiUnavailableError(`${method} ${path} returned ${response.status}`);

  const text = await response.text();
  const json: unknown = text ? safeJson(text) : null;
  if (response.ok) return { ok: true, status: response.status, body: json as T, setCookies: response.headers.getSetCookie() };
  return { ok: false, status: response.status, problem: toProblem(response.status, json) };
}

function safeJson(text: string): unknown {
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
}

/** Page reads: the body, or an error page for anything unexpected (a 404 is returned as null). */
export async function adminGet<T>(path: string): Promise<T | null> {
  const result = await adminFetch<T>('GET', path);
  if (result.ok) return result.body;
  if (result.status === 404) return null;
  if (result.status === 403) throw new Error('Your account lacks the permission for this page.');
  throw new Error(`GET ${path} returned ${result.status}`);
}

/** Refreshes `lastSeen` on activity; the proxy also does this for plain page views. */
export async function touchAdminSession() {
  const session = await readAdminSession();
  if (session) await writeSessionCookie(touchSession(session, Date.now()));
}
