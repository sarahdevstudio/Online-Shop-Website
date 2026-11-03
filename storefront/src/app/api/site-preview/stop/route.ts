import { NextResponse } from 'next/server';
import { PREVIEW_COOKIE, previewCookieOptions } from '@/lib/site-preview';
import { readSessionConfig } from '@/lib/admin/session';

/** Ends the draft preview (clears the cookie) and goes back to the admin, or to a safe internal `next` path. */
export function GET(request: Request) {
  const next = new URL(request.url).searchParams.get('next') ?? '/admin/site';
  const target = /^\/(?!\/)[A-Za-z0-9\-._~%/?=&#]*$/.test(next) ? next : '/admin/site';
  const response = NextResponse.redirect(new URL(target, request.url), 303);
  response.cookies.set(PREVIEW_COOKIE, '', { ...previewCookieOptions(readSessionConfig().production), maxAge: 0 });
  return response;
}
