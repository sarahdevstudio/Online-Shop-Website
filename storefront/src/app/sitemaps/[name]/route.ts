import { SITEMAP_NAMES, entriesFor, urlsetXml, XML_HEADERS, type SitemapName } from '@/lib/sitemaps';

export async function GET(_request: Request, { params }: { params: Promise<{ name: string }> }) {
  const name = (await params).name.replace(/\.xml$/, '') as SitemapName;
  if (!(SITEMAP_NAMES as readonly string[]).includes(name)) return new Response('Not found', { status: 404 });
  return new Response(urlsetXml(await entriesFor(name)), { headers: XML_HEADERS });
}
