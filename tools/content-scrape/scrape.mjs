// One-time import of the blog posts from ghr-co.com (WooCommerce/WordPress). Never run by the app or CI.
// Crawls politely (1 request/second, a plain User-Agent), writes WebP images and a manifest the
// `import-initial-content` command reads. Re-running against the same source produces the same manifest
// (posts sorted by slug, object keys sorted) so a re-scrape diffs cleanly.
//
//   node tools/content-scrape/scrape.mjs
//
// Reuses the storefront's own `sharp` dependency (no new dependency for this dev-only script).
import sharp from '../../storefront/node_modules/sharp/dist/index.cjs';
import { writeFileSync, mkdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { decodeEntities, sortKeysDeep } from '../catalog-scrape/scrape.mjs';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const OUT_DIR = join(ROOT, 'src', 'Api', 'SeedData', 'initial-content');
const IMAGES_DIR = join(OUT_DIR, 'images');
const BASE = 'https://ghr-co.com';
const USER_AGENT = 'shop-mvp-content-import';

// The source has no Latin slugs for its posts; English ASCII slugs are assigned here once, keyed by the
// percent-encoded Persian path segment the sitemap gives for each post (stable across re-scrapes).
const SLUG_BY_PATH = {
  '/%da%86%d9%86%d8%af-%d8%ae%d8%b7%d8%a7%db%8c-%d8%b1%d8%a7%db%8c%d8%ac-%d8%af%d8%b1-%d8%b1%d9%88%d8%b4-%d9%86%d9%88%d8%b1%d9%be%d8%b1%d8%af%d8%a7%d8%b2%db%8c-%d9%85%d9%86%d8%b2%d9%84/':
    'common-lighting-mistakes',
  '/%d8%a2%db%8c%d8%a7-%d8%b3%d9%84%d8%a7%d9%85%d8%aa-%d8%a7%d9%86%d8%b3%d8%a7%d9%86-%d8%a8%d8%a7-%d9%84%d8%a7%d9%85%d9%be%e2%80%8c%d9%87%d8%a7%db%8c-%da%a9%d9%85-%d9%85%d8%b5%d8%b1%d9%81-%d8%af%d8%b1/':
    'cfl-bulbs-health-risks',
  '/%d8%b1%d8%a7%d9%87%da%a9%d8%a7%d8%b1%d9%87%d8%a7%db%8c-%da%a9%d8%a7%d9%87%d8%b4-%d9%88-%d8%b5%d8%b1%d9%81%d9%87-%d8%ac%d9%88%db%8c%db%8c-%d9%85%d8%b5%d8%b1%d9%81-%d8%a8%d8%b1%d9%82-%d8%ae%d8%a7%d9%86/':
    'energy-saving-tips-home-office',
  '/%d8%b1%d9%88%d8%b4%e2%80%8c%d9%87%d8%a7%db%8c-%d8%b7%d8%b1%d8%a7%d8%ad%db%8c-%d8%af%da%a9%d9%88%d8%b1%d8%a7%d8%b3%db%8c%d9%88%d9%86-%d8%a7%d8%aa%d8%a7%d9%82-%d8%ae%d9%88%d8%a7%d8%a8%e2%80%8c%d9%87/':
    'windowless-bedroom-decor-lighting',
};

let lastRequestAt = 0;
async function politeFetch(url) {
  const wait = lastRequestAt + 1000 - Date.now();
  if (wait > 0) await new Promise((r) => setTimeout(r, wait));
  lastRequestAt = Date.now();
  const response = await fetch(url, { headers: { 'User-Agent': USER_AGENT } });
  if (!response.ok) throw new Error(`${response.status} ${response.statusText} fetching ${url}`);
  return response;
}

async function fetchText(url) {
  return (await politeFetch(url)).text();
}

function stripTags(html) {
  return decodeEntities(
    html
      .replace(/<script[\s\S]*?<\/script>/gi, '')
      .replace(/<style[\s\S]*?<\/style>/gi, '')
      .replace(/<[^>]+>/g, ' ')
  )
    .replace(/\s+/g, ' ')
    .trim();
}

// Exported for scrape.test.mjs. The theme's "latest posts" sidebar widget on /news/ is the only place a post's
// publish date is shown (the single-post template has the date display and the featured image turned off), and
// it also carries each post's feature image — so it is this scraper's one source for both.
export function parseNewsWidget(html) {
  const posts = {};
  const re = /<a href="https:\/\/ghr-co\.com(\/[^"]+\/)">[^<]*<div class="popular-featured-img" style="background-image: url\(([^)]+)\);"><\/div><span class="meta-wrap"><span class="post-title">([^<]+)<\/span> <span class="post-date">(\d{1,2} \w+ \d{4})<\/span>/g;
  let m;
  while ((m = re.exec(html))) {
    posts[m[1]] = { imageUrl: m[2], title: m[3].trim(), date: m[4] };
  }

  return posts;
}

// Exported for scrape.test.mjs. "01 September 2019" -> "2019-09-01T00:00:00Z".
export function parseEnglishDate(text) {
  const months = {
    January: '01', February: '02', March: '03', April: '04', May: '05', June: '06',
    July: '07', August: '08', September: '09', October: '10', November: '11', December: '12',
  };
  const match = /^(\d{1,2}) (\w+) (\d{4})$/.exec(text.trim());
  if (!match) throw new Error(`unrecognized date '${text}'`);
  const [, day, monthName, year] = match;
  const month = months[monthName];
  if (!month) throw new Error(`unrecognized month '${monthName}'`);
  return `${year}-${month}-${day.padStart(2, '0')}T00:00:00Z`;
}

// Exported for scrape.test.mjs. The excerpt is the body's first sentence, capped to fit BlogPost.ExcerptMaxLength
// (300); the source has no separate excerpt field.
export function excerptOf(body) {
  const firstSentence = /^[^.!?۔؟]*[.!?۔؟]/.exec(body)?.[0] ?? body;
  return firstSentence.length <= 300 ? firstSentence.trim() : `${firstSentence.slice(0, 297).trim()}...`;
}

function parsePostBody(html, path) {
  const divStart = html.indexOf('class="post-content"');
  const contentStart = html.indexOf('>', divStart) + 1;
  const contentEnd = html.indexOf('<div class="post-tags">', contentStart);
  if (divStart < 0 || contentStart < 1 || contentEnd < 0) throw new Error(`could not find the post body for ${path}`);
  const body = stripTags(html.slice(contentStart, contentEnd));
  if (!body) throw new Error(`empty post body for ${path}`);
  return body;
}

async function downloadAndConvert(url, destPath) {
  const buffer = Buffer.from(await (await politeFetch(url)).arrayBuffer());
  // sharp strips EXIF/ICC metadata by default (it is only kept when .withMetadata() is called).
  await sharp(buffer)
    .resize({ width: 1600, height: 1600, fit: 'inside', withoutEnlargement: true })
    .webp({ quality: 80 })
    .toFile(destPath);
}

async function main() {
  console.log(`fetching post list from ${BASE}/wp-sitemap-posts-post-1.xml`);
  const postSitemap = await fetchText(`${BASE}/wp-sitemap-posts-post-1.xml`);
  const postUrls = [...postSitemap.matchAll(/<loc>([^<]+)<\/loc>/g)].map((m) => m[1]);
  console.log(`${postUrls.length} post URLs found`);

  console.log(`fetching ${BASE}/news/ for titles, dates and feature images`);
  const newsHtml = await fetchText(`${BASE}/news/`);
  const widgetByPath = parseNewsWidget(newsHtml);

  mkdirSync(IMAGES_DIR, { recursive: true });

  const posts = [];
  const seenSlugs = new Set();
  for (const url of postUrls) {
    const path = new URL(url).pathname;
    const slug = SLUG_BY_PATH[path];
    if (!slug) throw new Error(`no English slug assigned for '${path}' — add it to SLUG_BY_PATH`);
    if (seenSlugs.has(slug)) throw new Error(`duplicate slug '${slug}' (from ${url})`);
    seenSlugs.add(slug);

    const widgetEntry = widgetByPath[path];
    if (!widgetEntry) throw new Error(`'${url}' is not listed in the /news/ latest-posts widget (title/date/image unavailable)`);

    console.log(`fetching ${url}`);
    const html = await fetchText(url);
    const body = parsePostBody(html, path);
    const publishedAtUtc = parseEnglishDate(widgetEntry.date);

    const file = `${slug}.webp`;
    console.log(`  image: ${widgetEntry.imageUrl}`);
    await downloadAndConvert(widgetEntry.imageUrl, join(IMAGES_DIR, file));

    posts.push({
      slug,
      title: widgetEntry.title,
      body,
      excerpt: excerptOf(body),
      publishedAtUtc,
      image: { file, alt: widgetEntry.title },
    });
  }

  posts.sort((a, b) => a.slug.localeCompare(b.slug));
  const manifest = sortKeysDeep({ posts });

  const manifestPath = join(OUT_DIR, 'manifest.json');
  writeFileSync(manifestPath, JSON.stringify(manifest, null, 2) + '\n');
  console.log(`wrote ${manifestPath} (${posts.length} posts)`);
}

// Only run the scrape when this file is executed directly, not when scrape.test.mjs imports the helpers.
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((err) => {
    console.error(err.message);
    process.exit(1);
  });
}
