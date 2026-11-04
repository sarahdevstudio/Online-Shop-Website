import { SITEMAP_NAMES, entriesFor, indexXml, XML_HEADERS } from '@/lib/sitemaps';

/** The sitemap index: one entry per non-empty sitemap, with the last change of its newest URL. */
export async function GET() {
  const parts = await Promise.all(SITEMAP_NAMES.map(async (name) => ({ name, entries: await entriesFor(name) })));
  return new Response(indexXml(parts), { headers: XML_HEADERS });
}
