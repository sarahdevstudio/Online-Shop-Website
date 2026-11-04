import { NextResponse } from 'next/server';
import { adminFetch, readAdminSession } from '@/lib/admin/api';
import { requestTooLarge } from '@/lib/admin/image-upload';
import { adminMessages } from '@/lib/admin/messages';
import { siteMessages } from '@/lib/admin/site-messages';
import type { SiteUploadResult } from '@/lib/admin/site-types';

/**
 * Site image upload (logo, favicon, hero, banner, share image). Same-origin multipart post with the `admin_session` cookie,
 * STREAMED to `POST /admin/site/images`; the API enforces type, size, dimensions (favicon: square PNG >= 192 px) and purpose.
 */
export async function POST(request: Request) {
  if (!(await readAdminSession())) return NextResponse.json({ ok: false, message: adminMessages.forbidden }, { status: 401 });

  const origin = request.headers.get('origin');
  if (origin && new URL(origin).host !== request.headers.get('host')) {
    return NextResponse.json({ ok: false, message: adminMessages.forbidden }, { status: 403 });
  }

  const contentType = request.headers.get('content-type') ?? '';
  if (!contentType.toLowerCase().startsWith('multipart/form-data') || !request.body) {
    return NextResponse.json({ ok: false, message: siteMessages.upload.failed }, { status: 400 });
  }
  const length = request.headers.get('content-length');
  if (requestTooLarge(length === null ? null : Number(length))) {
    return NextResponse.json({ ok: false, message: siteMessages.upload.tooBig }, { status: 413 });
  }

  const result = await adminFetch<SiteUploadResult>('POST', '/admin/site/images', {
    stream: { body: request.body, contentType, contentLength: length },
    timeoutMs: 60_000,
  });
  if (!result.ok) {
    const first = Object.values(result.problem.errors ?? {})[0]?.[0];
    const message = result.status === 413 ? siteMessages.upload.tooBig : (first || result.problem.detail) ?? siteMessages.upload.failed;
    return NextResponse.json({ ok: false, message }, { status: result.status === 413 ? 413 : 400 });
  }
  return NextResponse.json({ ok: true, message: siteMessages.upload.uploaded, image: result.body }, { status: 201 });
}
