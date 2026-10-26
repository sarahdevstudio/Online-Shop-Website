import { NextResponse, type NextRequest } from 'next/server';
import { ADMIN_COOKIE, readSessionConfig, sealSession, sessionCookieOptions, sessionState, touchSession, unsealSession } from './lib/admin/session';
import { securityHeaders } from './lib/security-headers';

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

export function proxy(request: NextRequest) {
  const { pathname } = request.nextUrl;
  const response = pathname === '/admin' || pathname.startsWith('/admin/') ? adminGate(request) : NextResponse.next();

  for (const [name, value] of Object.entries(securityHeaders(pathname))) response.headers.set(name, value);
  if (pathname === '/admin' || pathname.startsWith('/admin/')) {
    response.headers.set('X-Robots-Tag', 'noindex, nofollow'); // the admin is never indexed, in any environment
    response.headers.set('Cache-Control', 'no-store');
  }
  return response;
}

// All pages and route handlers except Next's static files and the admin upload handler (/admin/api/...), whose body must be
// streamed, not buffered by the proxy. That handler gets its headers from next.config.ts and checks the session itself.
export const config = { matcher: ['/((?!_next/static|_next/image|admin/api/).*)'] };
