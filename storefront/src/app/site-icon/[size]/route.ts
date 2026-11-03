import sharp from 'sharp';
import { apiBaseUrl } from '@/lib/api';
import { getSiteConfig } from '@/lib/site-config';

/**
 * Derives the 32 / 180 / 192 px icons from the published favicon (one square PNG uploaded in the admin). The browser asks
 * for these through the <link rel="icon"> / apple-touch-icon tags the root layout writes.
 */
const SIZES = new Set([32, 180, 192]);

export async function GET(_request: Request, context: { params: Promise<{ size: string }> }) {
  const { size: raw } = await context.params;
  const size = Number(raw);
  if (!SIZES.has(size)) return new Response(null, { status: 404 });

  const { theme } = await getSiteConfig();
  if (!theme.favicon) return new Response(null, { status: 404 });

  try {
    const source = await fetch(`${apiBaseUrl()}${theme.favicon.url}`, { signal: AbortSignal.timeout(5_000), next: { revalidate: 3600 } });
    if (!source.ok) return new Response(null, { status: 404 });
    const png = await sharp(Buffer.from(await source.arrayBuffer()))
      .resize(size, size, { fit: 'cover' })
      .png({ compressionLevel: 9 })
      .toBuffer();
    return new Response(new Uint8Array(png), { headers: { 'Content-Type': 'image/png', 'Cache-Control': 'public, max-age=3600' } });
  } catch {
    return new Response(null, { status: 404 });
  }
}
