import { NextResponse, type NextRequest } from 'next/server';
import { ADMIN_COOKIE, readSessionConfig, sealSession, sessionCookieOptions, sessionState, touchSession, unsealSession } from './lib/admin/session';
import { isReservedPath } from './lib/reserved-paths';
import { isResolvablePath } from './lib/route-paths';
import { contentSecurityPolicy, newNonce } from './lib/csp';
import { securityHeaders } from './lib/security-headers';
import { PREVIEW_COOKIE } from './lib/site-preview';

/**
 * Runs before every request (except static files and the streaming upload route):
 * 1. `/admin/**` gate: a missing, forged or expired (idle / absolute) session is sent to the login page and its cookie
 *    deleted; a valid one has its idle timer renewed. This is a convenience layer: every page, action and route handler also
 *    calls `requireAdmin()` itself.
 * 2. Headers on EVERY response: security headers, and noindex/nofollow unless SITE_ENV=production (see lib/security-headers).
 */
function adminGate(request: NextRequest, forward: Headers): NextResponse {
  const config = readSessionConfig();
  if (request.nextUrl.pathname === '/admin/login') return NextResponse.next({ request: { headers: forward } });

  const session = unsealSession(request.cookies.get(ADMIN_COOKIE)?.value, config.secret);
  if (!session || sessionState(session, Date.now(), config.limits) !== 'ok') {
    const redirect = NextResponse.redirect(new URL('/admin/login', request.url));
    redirect.cookies.set(ADMIN_COOKIE, '', { ...sessionCookieOptions(config), maxAge: 0 });
    return redirect;
  }

  const next = NextResponse.next({ request: { headers: forward } });
  next.cookies.set(ADMIN_COOKIE, sealSession(touchSession(session, Date.now()), config.secret), sessionCookieOptions(config));
  return next;
}

/**
 * A permanently deleted category or post answers 410, not 404 (docs/11 C-3). A Next page cannot choose its status, so the
 * proxy asks the API (an in-memory lookup there) about public-looking paths and, for a deleted one, rewrites to the /gone page
 * with status 410. "Gone" is permanent, so it is remembered for a minute; "not gone" is never remembered (a post deleted a
 * second ago must already answer 410). If the API cannot answer, lookups pause for a few seconds so a dead API does not delay
 * every request, and the catch-all page then answers 404 for such paths.
 */
const GONE_TTL_MS = 60_000;
const API_PAUSE_MS = 5_000;
const GONE_CACHE_MAX = 1000;
const goneCache = new Map<string, number>();
let apiPausedUntil = 0;

async function isGone(pathname: string): Promise<boolean> {
  if (!isResolvablePath(pathname) || isReservedPath(pathname)) return false;
  const now = Date.now();
  const goneAt = goneCache.get(pathname);
  if (goneAt !== undefined && now - goneAt < GONE_TTL_MS) return true;
  if (now < apiPausedUntil) return false;
  const base = process.env.API_BASE_URL ?? (process.env.NODE_ENV === 'development' ? 'http://localhost:5167' : '');
  if (!base) return false;
  try {
    const response = await fetch(`${base.replace(/\/+$/, '')}/api/v1/catalog/resolve?path=${encodeURIComponent(pathname)}`, {
      headers: { accept: 'application/json' },
      cache: 'no-store',
      signal: AbortSignal.timeout(1500),
    });
    if (!response.ok) throw new Error(`resolve returned ${response.status}`);
    const body = (await response.json()) as { result?: { kind?: string } | null };
    if (body.result?.kind !== 'gone') {
      goneCache.delete(pathname);
      return false;
    }
    if (goneCache.size >= GONE_CACHE_MAX) goneCache.clear();
    goneCache.set(pathname, now);
    return true;
  } catch {
    apiPausedUntil = now + API_PAUSE_MS;
    return false;
  }
}

export async function proxy(request: NextRequest) {
  const { pathname } = request.nextUrl;
  const isAdmin = pathname === '/admin' || pathname.startsWith('/admin/');
  const readOnly = request.method === 'GET' || request.method === 'HEAD';
  // The nonce goes to Next on the REQUEST (Next tags its own scripts with it) and the policy on the response.
  const nonce = newNonce();
  const csp = contentSecurityPolicy({ nonce, development: process.env.NODE_ENV === 'development' });
  const forward = new Headers(request.headers);
  forward.set('x-nonce', nonce);
  forward.set('content-security-policy', csp);
  const response = isAdmin
    ? adminGate(request, forward)
    : readOnly && (await isGone(pathname))
      ? NextResponse.rewrite(new URL('/gone', request.url), { status: 410, request: { headers: forward } })
      : NextResponse.next({ request: { headers: forward } });
  response.headers.set('Content-Security-Policy', csp);

  for (const [name, value] of Object.entries(securityHeaders(pathname))) response.headers.set(name, value);
  // A draft preview is for one admin only: never cached, never indexed.
  if (request.cookies.has(PREVIEW_COOKIE)) {
    response.headers.set('Cache-Control', 'private, no-store');
    response.headers.set('X-Robots-Tag', 'noindex, nofollow');
  }
  if (pathname === '/admin' || pathname.startsWith('/admin/')) {
    response.headers.set('X-Robots-Tag', 'noindex, nofollow'); // the admin is never indexed, in any environment
    response.headers.set('Cache-Control', 'no-store');
  }
  return response;
}

// All pages and route handlers except Next's static files and the admin upload handler (/admin/api/...), whose body must be
// streamed, not buffered by the proxy. That handler gets its headers from next.config.ts and checks the session itself.
export const config = { matcher: ['/((?!_next/static|_next/image|admin/api/).*)'] };
