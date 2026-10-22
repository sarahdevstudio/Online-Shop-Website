import { apiBaseUrl } from '@/lib/api';

/**
 * Product images live in the API's media folder (`/media/...`). This pass-through serves them from the site's own
 * origin, so the API can stay private and next/image can treat them as local images.
 * Only the image types the API stores are forwarded.
 */
const CONTENT_TYPES: Record<string, string> = { jpg: 'image/jpeg', png: 'image/png', webp: 'image/webp' };
const SEGMENT = /^[A-Za-z0-9_-]+(\.[A-Za-z0-9]+)?$/;

export async function GET(_request: Request, { params }: { params: Promise<{ path: string[] }> }) {
  const { path } = await params;
  const extension = path.at(-1)?.split('.').pop()?.toLowerCase() ?? '';
  if (!path.every((segment) => SEGMENT.test(segment)) || !CONTENT_TYPES[extension]) {
    return new Response(null, { status: 404 });
  }

  let upstream: Response;
  try {
    upstream = await fetch(`${apiBaseUrl()}/media/${path.join('/')}`, { cache: 'no-store', signal: AbortSignal.timeout(10_000) });
  } catch {
    return new Response(null, { status: 503, headers: { 'cache-control': 'no-store', 'retry-after': '60' } });
  }

  if (upstream.status === 404) return new Response(null, { status: 404 });
  if (!upstream.ok || !upstream.body) {
    return new Response(null, { status: 503, headers: { 'cache-control': 'no-store', 'retry-after': '60' } });
  }

  return new Response(upstream.body, {
    headers: {
      'content-type': CONTENT_TYPES[extension],
      // File names are random and never reused.
      'cache-control': 'public, max-age=31536000, immutable',
      'x-content-type-options': 'nosniff',
    },
  });
}
