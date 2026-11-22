import { NextResponse } from 'next/server';
import { adminFetch, readAdminSession } from '@/lib/admin/api';
import { MAX_IMAGE_BYTES, requestTooLarge } from '@/lib/admin/image-upload';
import { processLogo, processSitePhoto } from '@/lib/admin/image-pipeline';
import { adminMessages } from '@/lib/admin/messages';
import { siteMessages } from '@/lib/admin/site-messages';
import type { SiteUploadResult } from '@/lib/admin/site-types';

/**
 * Site image upload (logo, favicon, hero, banner, share image). Same-origin multipart post with the `admin_session`
 * cookie. Hero/banner/share ("og") photos and non-transparent logos are processed with sharp (auto-rotate, strip
 * metadata, resize, convert to WebP) before forwarding; a logo with transparency stays lossless PNG, since WebP's
 * lossy mode would fringe the alpha edge; the favicon is forwarded unprocessed because the API requires an exact
 * square PNG >= 192px for it (see docs/08). The API still enforces type, size, dimensions and purpose.
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

  const form = await request.formData();
  const file = form.get('file');
  const alt = form.get('alt');
  const purpose = form.get('purpose');
  if (!(file instanceof File) || typeof purpose !== 'string') {
    return NextResponse.json({ ok: false, message: siteMessages.upload.failed }, { status: 400 });
  }

  const input = Buffer.from(await file.arrayBuffer());
  if (input.length === 0 || input.length > MAX_IMAGE_BYTES) {
    return NextResponse.json({ ok: false, message: siteMessages.upload.tooBig }, { status: 413 });
  }

  let outBuffer: Buffer;
  let outType: string;
  let outName: string;
  if (purpose === 'favicon') {
    // The API enforces an exact square PNG here; any re-encoding risks breaking that.
    outBuffer = input;
    outType = file.type || 'image/png';
    outName = file.name || 'favicon.png';
  } else {
    try {
      if (purpose === 'logo') {
        outBuffer = await processLogo(input);
        const isPng = outBuffer.subarray(0, 8).equals(Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]));
        outType = isPng ? 'image/png' : 'image/webp';
        outName = isPng ? 'logo.png' : 'logo.webp';
      } else {
        outBuffer = await processSitePhoto(input);
        outType = 'image/webp';
        outName = `${purpose}.webp`;
      }
    } catch {
      return NextResponse.json({ ok: false, message: siteMessages.upload.failed }, { status: 400 });
    }
  }

  const outgoing = new FormData();
  outgoing.set('file', new Blob([new Uint8Array(outBuffer)], { type: outType }), outName);
  outgoing.set('purpose', purpose);
  if (typeof alt === 'string' && alt) outgoing.set('alt', alt);
  const multipart = new Request('http://upload.local/', { method: 'POST', body: outgoing });
  const outBody = Buffer.from(await multipart.arrayBuffer());

  const result = await adminFetch<SiteUploadResult>('POST', '/admin/site/images', {
    stream: { body: new Response(outBody).body!, contentType: multipart.headers.get('content-type')!, contentLength: String(outBody.length) },
    timeoutMs: 60_000,
  });
  if (!result.ok) {
    const first = Object.values(result.problem.errors ?? {})[0]?.[0];
    const message = result.status === 413 ? siteMessages.upload.tooBig : (first || result.problem.detail) ?? siteMessages.upload.failed;
    return NextResponse.json({ ok: false, message }, { status: result.status === 413 ? 413 : 400 });
  }
  return NextResponse.json({ ok: true, message: siteMessages.upload.uploaded, image: result.body }, { status: 201 });
}
