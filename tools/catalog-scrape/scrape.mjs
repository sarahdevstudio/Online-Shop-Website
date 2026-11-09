// One-time import of the real catalog from ghr-co.com (WooCommerce/WordPress). Never run by the app or CI.
// Crawls politely (1 request/second, a plain User-Agent), writes WebP images and a manifest the
// `import-initial-catalog` command reads. Re-running against the same source produces the same manifest
// (products sorted by slug, object keys sorted) so a re-scrape diffs cleanly.
//
//   node tools/catalog-scrape/scrape.mjs
//
// Reuses the storefront's own `sharp` dependency (no new dependency for this dev-only script).
import sharp from '../../storefront/node_modules/sharp/dist/index.cjs';
import { writeFileSync, mkdirSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const OUT_DIR = join(ROOT, 'src', 'Api', 'SeedData', 'initial-catalog');
const IMAGES_DIR = join(OUT_DIR, 'images');
const BASE = 'https://ghr-co.com';
const USER_AGENT = 'shop-mvp-catalog-import';

// The source has exactly one product category; its Persian name is scraped, its slug (no Latin source
// text exists for it) is assigned here once.
const CATEGORY_SLUG = 'pendant-chandeliers';

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
  return html
    .replace(/<br\s*\/?>/gi, '\n')
    .replace(/<\/p>/gi, '\n')
    .replace(/<[^>]+>/g, '')
    .replace(/&nbsp;/g, ' ')
    .replace(/&amp;/g, '&')
    .replace(/&quot;/g, '"')
    .split('\n')
    .map((line) => line.trim())
    .filter((line) => line.length > 0)
    .join('\n');
}

function slugify(sku) {
  return sku
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
}

function parseProduct(html, sourceUrl) {
  const name = /<h1 class="product_title[^"]*"[^>]*>([^<]+)<\/h1>/.exec(html)?.[1].trim();
  if (!name) throw new Error(`could not find the product title in ${sourceUrl}`);

  const descriptionHtml = /<div class="woocommerce-product-details__short-description">([\s\S]*?)<\/div>/.exec(html)?.[1] ?? '';
  const description = stripTags(descriptionHtml);

  // Most products have a <span class="sku">; a few only state it in the short description ("کد کالا: X" /
  // "کدکالا : X").
  let sku = /<span class="sku[^"]*">([^<]*)<\/span>/.exec(html)?.[1].trim();
  if (!sku) sku = /کد\s*کالا\s*[:：]?\s*([A-Za-z0-9/.-]+)/.exec(descriptionHtml)?.[1];
  // Last resort: the model code the Persian title itself carries (e.g. "لوستر MLK-5-15").
  if (!sku) sku = /[A-Za-z0-9][A-Za-z0-9/.-]*/.exec(name)?.[0];
  if (!sku) throw new Error(`could not find a product code in ${sourceUrl} (needed for the slug)`);
  const slug = slugify(sku);
  if (!/^[a-z0-9]+(-[a-z0-9]+)*$/.test(slug)) throw new Error(`product code '${sku}' in ${sourceUrl} did not produce a valid slug`);

  const priceMatch = /<span class="woocommerce-Price-amount amount">([\d,]+)/.exec(html);
  const priceRial = priceMatch ? Number(priceMatch[1].replace(/,/g, '')) : null;
  if (priceRial !== null && priceRial % 10 !== 0) throw new Error(`price ${priceRial} Rial in ${sourceUrl} is not a whole Toman amount`);
  const priceToman = priceRial === null ? null : priceRial / 10;

  const specs = {};
  const specRegex = /<tr class="woocommerce-product-attributes-item[^"]*">\s*<th class="woocommerce-product-attributes-item__label">([^<]+)<\/th>\s*<td class="woocommerce-product-attributes-item__value">([\s\S]*?)<\/td>\s*<\/tr>/g;
  for (const [, label, valueHtml] of html.matchAll(specRegex)) {
    specs[label.trim()] = stripTags(valueHtml);
  }

  // The gallery sits between its own marker and the "related products" section further down the page;
  // restricting the search to that span keeps related-product thumbnails out of this product's image list.
  const galleryStart = html.indexOf('woocommerce-product-gallery');
  const galleryEnd = html.indexOf('related products', galleryStart);
  if (galleryStart < 0 || galleryEnd < 0) throw new Error(`could not find the image gallery in ${sourceUrl}`);
  const galleryHtml = html.slice(galleryStart, galleryEnd);
  const images = [...galleryHtml.matchAll(/href="(https:\/\/ghr-co\.com\/wp-content\/uploads\/[^"]+\.(?:jpe?g|png|webp))"/gi)]
    .map((m) => m[1])
    .filter((url) => !/-\d+x\d+\.(?:jpe?g|png|webp)$/i.test(url));
  const uniqueImages = [...new Set(images)];
  if (uniqueImages.length === 0) throw new Error(`no images found in ${sourceUrl}`);

  return { sourceUrl, slug, name, categorySlug: CATEGORY_SLUG, priceToman, specs, description, imageUrls: uniqueImages };
}

function sortKeysDeep(value) {
  if (Array.isArray(value)) return value.map(sortKeysDeep);
  if (value && typeof value === 'object') {
    return Object.fromEntries(Object.keys(value).sort().map((key) => [key, sortKeysDeep(value[key])]));
  }
  return value;
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
  console.log(`fetching product list from ${BASE}/wp-sitemap-posts-product-1.xml`);
  const productSitemap = await fetchText(`${BASE}/wp-sitemap-posts-product-1.xml`);
  const productUrls = [...productSitemap.matchAll(/<loc>([^<]+)<\/loc>/g)].map((m) => m[1]);
  console.log(`${productUrls.length} product URLs found`);

  console.log(`fetching the category page`);
  const categorySitemap = await fetchText(`${BASE}/wp-sitemap-taxonomies-product_cat-1.xml`);
  const categoryUrl = /<loc>([^<]+)<\/loc>/.exec(categorySitemap)?.[1];
  if (!categoryUrl) throw new Error('no product category found');
  const categoryHtml = await fetchText(categoryUrl);
  const categoryName = /<h1[^>]*>([^<]+)<\/h1>/.exec(categoryHtml)?.[1].trim();
  if (!categoryName) throw new Error(`could not find the category name at ${categoryUrl}`);

  mkdirSync(IMAGES_DIR, { recursive: true });

  const products = [];
  const seenSlugs = new Set();
  for (const url of productUrls) {
    console.log(`fetching ${url}`);
    const html = await fetchText(url);
    const product = parseProduct(html, url);
    if (seenSlugs.has(product.slug)) throw new Error(`duplicate slug '${product.slug}' (from ${url})`);
    seenSlugs.add(product.slug);

    const images = [];
    for (const [i, imageUrl] of product.imageUrls.entries()) {
      const file = `${product.slug}-${i + 1}.webp`;
      console.log(`  image ${i + 1}/${product.imageUrls.length}: ${imageUrl}`);
      await downloadAndConvert(imageUrl, join(IMAGES_DIR, file));
      images.push({ file, alt: product.name });
    }

    const { imageUrls, ...rest } = product;
    products.push({ ...rest, images });
  }

  products.sort((a, b) => a.slug.localeCompare(b.slug));
  const manifest = sortKeysDeep({
    categories: [{ slug: CATEGORY_SLUG, name: categoryName, parentSlug: null }],
    products,
  });

  const manifestPath = join(OUT_DIR, 'manifest.json');
  writeFileSync(manifestPath, JSON.stringify(manifest, null, 2) + '\n');
  console.log(`wrote ${manifestPath} (${products.length} products)`);
}

main().catch((err) => {
  console.error(err.message);
  process.exit(1);
});
