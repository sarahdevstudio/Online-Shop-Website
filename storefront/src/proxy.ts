import { NextResponse, type NextRequest } from 'next/server';
import { ADMIN_COOKIE, readSessionConfig, sealSession, sessionCookieOptions, sessionState, touchSession, unsealSession } from './lib/admin/session';
import { isReservedPath } from './lib/reserved-paths';
import { isResolvablePath } from './lib/route-paths';
import { securityHeaders } from './lib/security-headers';
import { PREVIEW_COOKIE } from './lib/site-preview';

/**
 * Runs before every request (except static files and the streaming upload route):
 * 1. `/admin/**` gate: a missing, forged or expired (idle / absolute) session is sent to the login page and its cookie
 *    deleted; a valid one has its idle timer renewed. This is a convenience layer: every page, action and route handler also
 *    calls `requireAdmin()` itself.
 * 2. Headers on EVERY response: security headers, and noindex/nofollow unless SITE_ENV=production (see lib/security-headers).
 */
function adminGate(request: NextRequest): NextResponse {
  const config = readSessionConfig();
  if (request.nextUrl.pathname === '/admin/login') return NextResponse.next();

  const session = unsealSession(request.cookies.get(ADMIN_COOKIE)?.value, config.secret);
  if (!session || sessionState(session, Date.now(), config.limits) !== 'ok') {
    const redirect = NextResponse.redirect(new URL('/admin/login', request.url));
    redirect.cookies.set(ADMIN_COOKIE, '', { ...sessionCookieOptions(config), maxAge: 0 });
    return redirect;
  }

  const next = NextResponse.next();
  next.cookies.set(ADMIN_COOKIE, sealSession(touchSession(session, Date.now()), config.secret), sessionCookieOptions(config));
  return next;
}

/**
 * A permanently deleted category or post answers 410, not 404 (docs/11 C-3). A Next page cannot choose its status, so the
 * proxy asks the API (an in-memory lookup there) about public-looking paths and, for a deleted one, rewrites to the /gone page
 * with status 410. Answers are kept briefly per worker; any failure means "not gone": the catch-all page then answers 404.
 */
// Short for "not gone" (an entity may be deleted any second and must turn into a 410 quickly), long for "gone" (permanent).
const NOT_GONE_TTL_MS = 3_000;
const GONE_TTL_MS = 60_000;
const GONE_CACHE_MAX = 1000;
const goneCache = new Map<string, { gone: boolean; at: number }>();

async function isGone(pathname: string): Promise<boolean> {
  if (!isResolvablePath(pathname) || isReservedPath(pathname)) return false;
  const now = Date.now();
  const hit = goneCache.get(pathname);
  if (hit && now - hit.at < (hit.gone ? GONE_TTL_MS : NOT_GONE_TTL_MS)) return hit.gone;
  const base = process.env.API_BASE_URL ?? (process.env.NODE_ENV === 'development' ? 'http://localhost:5167' : '');
  if (!base) return false;
  try {
    const response = await fetch(`${base.replace(/\/+$/, '')}/api/v1/catalog/resolve?path=${encodeURIComponent(pathname)}`, {
      headers: { accept: 'application/json' },
      cache: 'no-store',
      signal: AbortSignal.timeout(1500),
    });
    if (!response.ok) return false;
    const body = (await response.json()) as { result?: { kind?: string } | null };
    const gone = body.result?.kind === 'gone';
    if (goneCache.size >= GONE_CACHE_MAX) goneCache.clear();
    goneCache.set(pathname, { gone, at: now });
    return gone;
  } catch {
    return false;
  }
}

export async function proxy(request: NextRequest) {
  const { pathname } = request.nextUrl;
  const isAdmin = pathname === '/admin' || pathname.startsWith('/admin/');
  const readOnly = request.method === 'GET' || request.method === 'HEAD';
  const response = isAdmin
    ? adminGate(request)
    : readOnly && (await isGone(pathname))
      ? NextResponse.rewrite(new URL('/gone', request.url), { status: 410 })
      : NextResponse.next();

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
