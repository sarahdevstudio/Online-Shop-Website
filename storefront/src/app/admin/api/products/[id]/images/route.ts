import { NextResponse } from 'next/server';
import { adminFetch, readAdminSession } from '@/lib/admin/api';
import { MAX_IMAGE_BYTES, requestTooLarge, uploadErrorMessage } from '@/lib/admin/image-upload';
import { ImageTooSmallError, MIN_PRODUCT_SHORT_EDGE, processProductImage } from '@/lib/admin/image-pipeline';
import { adminMessages } from '@/lib/admin/messages';

/**
 * Image upload. The browser posts the multipart form here (same origin, `admin_session` cookie); the file is processed
 * with sharp (auto-rotate, strip metadata, resize, convert to WebP) before being forwarded to the API as a new multipart
 * body. The API still validates the result by magic bytes (ImageProbe / CatalogAdminService) — the browser is not trusted,
 * and neither is this route's own conversion.
 */
export async function POST(request: Request, { params }: { params: Promise<{ id: string }> }) {
  if (!(await readAdminSession())) return NextResponse.json({ ok: false, message: adminMessages.forbidden }, { status: 401 });

  // CSRF: a cross-site form post carries a foreign Origin; the cookie is SameSite=Strict as well.
  const origin = request.headers.get('origin');
  if (origin && new URL(origin).host !== request.headers.get('host')) {
    return NextResponse.json({ ok: false, message: adminMessages.forbidden }, { status: 403 });
  }

  const id = Number((await params).id);
  const contentType = request.headers.get('content-type') ?? '';
  if (!Number.isSafeInteger(id) || id <= 0 || !contentType.toLowerCase().startsWith('multipart/form-data') || !request.body) {
    return NextResponse.json({ ok: false, message: adminMessages.products.imageFailed }, { status: 400 });
  }

  const length = request.headers.get('content-length');
  if (requestTooLarge(length === null ? null : Number(length))) {
    return NextResponse.json({ ok: false, message: adminMessages.products.imageTooBig }, { status: 413 });
  }

  const form = await request.formData();
  const file = form.get('file');
  const alt = form.get('alt');
  if (!(file instanceof File)) {
    return NextResponse.json({ ok: false, message: adminMessages.products.imageFailed }, { status: 400 });
  }

  const input = Buffer.from(await file.arrayBuffer());
  if (input.length === 0 || input.length > MAX_IMAGE_BYTES) {
    return NextResponse.json({ ok: false, message: adminMessages.products.imageTooBig }, { status: 413 });
  }

  let processed: Buffer;
  try {
    processed = await processProductImage(input);
  } catch (error) {
    if (error instanceof ImageTooSmallError) {
      return NextResponse.json({ ok: false, message: adminMessages.products.imageTooSmall(String(MIN_PRODUCT_SHORT_EDGE)) }, { status: 400 });
    }
    return NextResponse.json({ ok: false, message: adminMessages.products.imageBadType }, { status: 400 });
  }

  const outgoing = new FormData();
  outgoing.set('file', new Blob([new Uint8Array(processed)], { type: 'image/webp' }), 'image.webp');
  if (typeof alt === 'string' && alt) outgoing.set('alt', alt);
  // A Request built from a FormData body computes the multipart boundary and Content-Type for us.
  const multipart = new Request('http://upload.local/', { method: 'POST', body: outgoing });
  const outBody = Buffer.from(await multipart.arrayBuffer());

  const result = await adminFetch<unknown>('POST', `/admin/products/${id}/images`, {
    stream: { body: new Response(outBody).body!, contentType: multipart.headers.get('content-type')!, contentLength: String(outBody.length) },
    timeoutMs: 60_000,
  });
  if (!result.ok) {
    return NextResponse.json({ ok: false, message: uploadErrorMessage(result.status, result.problem.errors) }, { status: result.status === 413 ? 413 : 400 });
  }
  return NextResponse.json({ ok: true, message: adminMessages.products.imageUploaded }, { status: 201 });
}
