import { NextResponse } from 'next/server';
import { adminDownload, readAdminSession } from '@/lib/admin/api';
import { adminMessages } from '@/lib/admin/messages';

/** The product table as a download. A GET that changes nothing but the API's audit row; the session cookie is checked here and by the API. */
export async function GET(request: Request) {
  if (!(await readAdminSession())) return NextResponse.json({ ok: false, message: adminMessages.forbidden }, { status: 401 });
  const format = new URL(request.url).searchParams.get('format') === 'xlsx' ? 'xlsx' : 'csv';
  const file = await adminDownload(`/admin/catalog/export?format=${format}`);
  if (!file.ok) return NextResponse.json({ ok: false, message: adminMessages.failed }, { status: file.status === 403 ? 403 : 502 });
  return new NextResponse(file.body, {
    headers: {
      'content-type': file.contentType,
      'content-disposition': file.disposition ?? `attachment; filename="products.${format}"`,
      'cache-control': 'no-store',
    },
  });
}
