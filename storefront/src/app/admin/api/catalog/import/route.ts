import { NextResponse } from 'next/server';
import { adminFetch, readAdminSession } from '@/lib/admin/api';
import { adminMessages } from '@/lib/admin/messages';
import { transferMessages } from '@/lib/admin/transfer-messages';

/** The API's own limit (2 MB) plus the multipart envelope; the API enforces it again. */
const MAX_BYTES = 2 * 1024 * 1024 + 64 * 1024;

/**
 * Import: `?mode=preview` (dry run, writes nothing) or `?mode=apply`. The multipart body is streamed to the API, which reads the
 * file, validates every row and answers with the preview or the result. Same-origin only (Origin header and a SameSite=Strict cookie).
 */
export async function POST(request: Request) {
  if (!(await readAdminSession())) return NextResponse.json({ ok: false, message: adminMessages.forbidden }, { status: 401 });
  const origin = request.headers.get('origin');
  if (origin && new URL(origin).host !== request.headers.get('host')) {
    return NextResponse.json({ ok: false, message: adminMessages.forbidden }, { status: 403 });
  }

  const mode = new URL(request.url).searchParams.get('mode') === 'apply' ? 'apply' : 'preview';
  const contentType = request.headers.get('content-type') ?? '';
  const length = request.headers.get('content-length');
  if (!contentType.toLowerCase().startsWith('multipart/form-data') || !request.body) {
    return NextResponse.json({ ok: false, message: transferMessages.failed }, { status: 400 });
  }
  if (length !== null && Number(length) > MAX_BYTES) {
    return NextResponse.json({ ok: false, message: transferMessages.tooBig }, { status: 413 });
  }

  const result = await adminFetch<unknown>('POST', `/admin/catalog/import/${mode}`, {
    stream: { body: request.body, contentType, contentLength: length },
    timeoutMs: 120_000,
  });
  if (!result.ok) {
    const file = (result.problem.errors?.file ?? result.problem.errors?.File ?? [])[0];
    const message = result.status === 403 ? adminMessages.forbidden : file ?? result.problem.detail ?? transferMessages.failed;
    return NextResponse.json({ ok: false, message }, { status: result.status >= 400 && result.status < 500 ? result.status : 502 });
  }
  return NextResponse.json({ ok: true, mode, result: result.body });
}
