import { NextResponse, type NextRequest } from 'next/server';
import { ADMIN_COOKIE, readSessionConfig, sealSession, sessionCookieOptions, sessionState, touchSession, unsealSession } from './lib/admin/session';

/**
 * Admin panel gate, run before every `/admin/**` request: a missing, forged or expired (idle / absolute) session is sent to
 * the login page and its cookie deleted; a valid one has its idle timer renewed. This is a convenience layer: every page,
 * action and route handler also calls `requireAdmin()` itself.
 */
export function proxy(request: NextRequest) {
  const config = readSessionConfig();
  const { pathname } = request.nextUrl;
  const response = (() => {
    if (pathname === '/admin/login') return NextResponse.next();

    const session = unsealSession(request.cookies.get(ADMIN_COOKIE)?.value, config.secret);
    if (!session || sessionState(session, Date.now(), config.limits) !== 'ok') {
      const redirect = NextResponse.redirect(new URL('/admin/login', request.url));
      redirect.cookies.set(ADMIN_COOKIE, '', { ...sessionCookieOptions(config), maxAge: 0 });
      return redirect;
    }

    const next = NextResponse.next();
    next.cookies.set(ADMIN_COOKIE, sealSession(touchSession(session, Date.now()), config.secret), sessionCookieOptions(config));
    return next;
  })();

  response.headers.set('X-Robots-Tag', 'noindex, nofollow');
  response.headers.set('Cache-Control', 'no-store');
  return response;
}

// Everything under /admin except the upload route handler (/admin/api/...), whose body must be streamed, not buffered.
export const config = { matcher: ['/admin', '/admin/((?!api/).*)'] };
