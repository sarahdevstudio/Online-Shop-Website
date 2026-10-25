import { NextResponse } from 'next/server';
import { adminFetch, readAdminSession } from '@/lib/admin/api';
import { requestTooLarge, uploadErrorMessage } from '@/lib/admin/image-upload';
import { adminMessages } from '@/lib/admin/messages';

/**
 * Image upload. The browser posts the multipart form here (same origin, `admin_session` cookie) and the body is STREAMED to
 * the API: no 1 MB Server Action body limit, nothing buffered in memory. The API enforces the real size and type rules.
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

  const result = await adminFetch<unknown>('POST', `/admin/products/${id}/images`, {
    stream: { body: request.body, contentType, contentLength: length },
    timeoutMs: 60_000,
  });
  if (!result.ok) {
    return NextResponse.json({ ok: false, message: uploadErrorMessage(result.status, result.problem.errors) }, { status: result.status === 413 ? 413 : 400 });
  }
  return NextResponse.json({ ok: true, message: adminMessages.products.imageUploaded }, { status: 201 });
}
