// Browser smoke for dynamic routing (docs/13-dynamic-routing.md). Drives a RUNNING stack (API in Development with the sample
// catalog + the built storefront) with Playwright and prints one PASS/FAIL line per check; exit code 1 when any check fails.
//
//   BASE=http://localhost:3000 API=http://127.0.0.1:5167 ADMIN_USER=… ADMIN_PASS=… OUT=./smoke-out \
//   AXE=/path/to/axe.min.js node scripts/smoke-routing.cjs
//
// It changes data (a product path, a category path, one blog post that it deletes again) and leaves the old paths as redirects,
// so run it against a development/staging database. Playwright is not a dependency of the storefront; use any installation.
const { chromium } = require('playwright');
const fs = require('node:fs');
const path = require('node:path');

const BASE = (process.env.BASE ?? 'http://localhost:3000').replace(/\/$/, '');
const API = (process.env.API ?? 'http://127.0.0.1:5167').replace(/\/$/, '');
const USER = process.env.ADMIN_USER ?? 'smoke-admin';
const PASS = process.env.ADMIN_PASS ?? 'Smoke-Admin-Pass-2026!';
const OUT = process.env.OUT ?? path.resolve('smoke-out');
const AXE = process.env.AXE ? fs.readFileSync(process.env.AXE, 'utf8') : null;
const RUN = Date.now().toString(36);

fs.mkdirSync(OUT, { recursive: true });
const results = [];
const check = (name, ok, detail = '') => {
  results.push({ name, ok, detail });
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${name}${detail ? `  — ${detail}` : ''}`);
};
const eq = (name, actual, expected) => check(name, actual === expected, actual === expected ? String(actual) : `expected ${expected}, got ${actual}`);

async function api(pathname) {
  const response = await fetch(`${API}/api/v1${pathname}`);
  return (await response.json()).result;
}

/** Status and Location of the first response, without following redirects. */
async function raw(context, url) {
  const response = await context.request.get(`${BASE}${url}`, { maxRedirects: 0 });
  return { status: response.status(), location: response.headers().location ?? null, body: await response.text() };
}

async function canonicalOf(page) {
  return page.locator('link[rel="canonical"]').first().getAttribute('href').catch(() => null);
}

async function jsonLd(page) {
  const blocks = await page.locator('script[type="application/ld+json"]').allTextContents();
  return blocks.join('\n');
}

async function shot(page, name) {
  await page.screenshot({ path: path.join(OUT, `${name}.png`), fullPage: true });
}

async function axeCheck(page, name) {
  if (!AXE) return;
  await page.evaluate(AXE);
  const result = await page.evaluate(() =>
    axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa', 'best-practice'] } }),
  );
  const violations = result.violations.map((v) => `${v.id} (${v.nodes.length})`);
  check(`axe: ${name}`, violations.length === 0, violations.join(', ') || `${result.passes.length} rules passed`);
}

async function login(page) {
  await page.goto(`${BASE}/admin/login`);
  await page.fill('input[name="username"]', USER);
  await page.fill('input[name="password"]', PASS);
  await Promise.all([page.waitForURL(/\/admin(\/)?$/), page.click('form button[type="submit"]')]);
}

/** Saves the form that contains the path field and waits until the server action finished. */
async function savePathForm(page) {
  const submit = page.locator('form:has(input[name="path"]) button[type="submit"]').first();
  await Promise.all([page.waitForLoadState('networkidle'), submit.click()]);
  await page.waitForTimeout(400);
}

(async () => {
  const browser = await chromium.launch();
  const context = await browser.newContext({ locale: 'fa-IR', viewport: { width: 1280, height: 900 } });
  const page = await context.newPage();
  const consoleErrors = [];
  page.on('pageerror', (error) => consoleErrors.push(error.message));

  // ---- 1. every existing public URL keeps working unchanged --------------------------------------------------------
  const tree = await api('/catalog/categories');
  const categories = tree.flatMap((c) => [c, ...c.children]);
  const cards = [];
  for (let p = 1; ; p++) {
    const list = await api(`/catalog/products?pageSize=60&page=${p}`);
    cards.push(...list.items);
    if (cards.length >= list.total || list.items.length === 0) break;
  }
  const pages = await api('/catalog/pages');
  const urls = [
    ...categories.map((c) => ({ url: `/category/${c.slug}`, path: c.path })),
    ...cards.map((c) => ({ url: `/product/${c.slug}`, path: c.path })),
    ...pages.map((p) => ({ url: `/${p.slug}`, path: p.path })),
    { url: '/', path: '/' },
    { url: '/blog', path: '/blog' },
  ];
  let broken = [];
  for (const entry of urls) {
    const r = await raw(context, entry.url);
    if (r.status !== 200 || entry.path !== entry.url) broken.push(`${entry.url} → ${r.status}${r.location ? ` ${r.location}` : ''} (path ${entry.path})`);
  }
  check(`all ${urls.length} existing URLs (${categories.length} categories, ${cards.length} products, ${pages.length} info pages, home, blog) answer 200 at their original address`, broken.length === 0, broken.slice(0, 5).join('; '));

  // ---- 2. sign in ------------------------------------------------------------------------------------------------------
  await login(page);
  check('admin sign-in', page.url().includes('/admin'));

  // ---- 3. change a product's path -------------------------------------------------------------------------------------
  const product = cards.find((c) => c.inStock) ?? cards[0];
  const full = await api(`/catalog/products/${product.slug}`);
  const admin = await context.request.get(`${BASE}/admin/products`); // find the id through the admin list
  const idMatch = new RegExp(`/admin/products/(\\d+)"[^>]*>[^<]*${product.name.slice(0, 6).replace(/[()[\]{}.*+?^$|\\]/g, '\\$&')}`).exec(await admin.text());
  const productId = idMatch ? idMatch[1] : null;
  check('found the product in the admin list', Boolean(productId), product.slug);
  const oldPath = product.path;
  const newPath = `/lustre/smoke-${RUN}`;

  await page.goto(`${BASE}/admin/products/${productId}`);
  await shot(page, '01-admin-product-edit-before');
  eq('product edit form shows the current path', await page.inputValue('input[name="path"]'), oldPath);
  await page.fill('input[name="path"]', newPath.toUpperCase() + '/'); // typed sloppily: upper case and a trailing slash
  const preview = await page.locator('bdi[aria-live="polite"]').first().textContent();
  check('the form previews the resulting URL', preview === `${BASE}${newPath}`, preview ?? '');
  await savePathForm(page);
  await page.reload();
  eq('the new path is saved (normalized)', await page.inputValue('input[name="path"]'), newPath);
  await shot(page, '02-admin-product-edit-after');
  await axeCheck(page, `/admin/products/${productId} (path field, old paths list)`);

  // old URL → 308 → new URL
  const hop = await raw(context, oldPath);
  eq('old product URL answers 308', hop.status, 308);
  eq('… to the new path', hop.location && new URL(hop.location, BASE).pathname, newPath);
  const query = await raw(context, `${oldPath}?wattage=9w&sort=x`);
  eq('the query string survives the redirect', query.location && new URL(query.location, BASE).search, '?wattage=9w&sort=x');
  await page.goto(`${BASE}${oldPath}`);
  eq('in the browser the old URL ends on the new URL', new URL(page.url()).pathname, newPath);
  eq('… with status 200 (no chain)', (await raw(context, newPath)).status, 200);
  await shot(page, '03-product-after-redirect');
  eq('canonical is the new URL', await canonicalOf(page), `${BASE}${newPath}`);
  check('JSON-LD uses the new path and not the old one', (await jsonLd(page)).includes(newPath) && !(await jsonLd(page)).includes(oldPath));
  const sitemap = (await context.request.get(`${BASE}/sitemap.xml`)).text();
  const sitemapText = await sitemap;
  check('sitemap lists the new URL', sitemapText.includes(`${BASE}${newPath}</loc>`));
  check('sitemap no longer lists the old URL', !sitemapText.includes(`${BASE}${oldPath}</loc>`));
  // internal links: the product's category page links to the new path
  const categoryPath = full.breadcrumb.at(-1).path;
  await page.goto(`${BASE}${categoryPath}`);
  check('category listing links to the new product path', (await page.locator(`a[href="${newPath}"], a[href^="${newPath}?"]`).count()) > 0);
  check('… and not to the old one', (await page.locator(`a[href="${oldPath}"], a[href^="${oldPath}?"]`).count()) === 0);

  // trailing slash and case variants
  const slash = await raw(context, `${newPath}/`);
  check('trailing-slash variant redirects to the clean URL', slash.status === 308 && new URL(slash.location, BASE).pathname === newPath, `${slash.status} ${slash.location}`);
  const upper = await raw(context, newPath.toUpperCase());
  check('upper-case variant redirects to the clean URL', upper.status === 308 && new URL(upper.location, BASE).pathname === newPath, `${upper.status} ${upper.location}`);

  // ---- 4. conflicts and reserved paths show Persian errors in the form -----------------------------------------------
  await page.goto(`${BASE}/admin/products/${productId}`);
  const other = cards.find((c) => c.slug !== product.slug);
  await page.fill('input[name="path"]', other.path);
  await savePathForm(page);
  let error = await page.locator('.ui-error').first().textContent().catch(() => null);
  check('a path owned by another product is refused with a Persian message naming it', Boolean(error) && /[؀-ۿ]/.test(error) && error.includes(other.name.slice(0, 5)), error ?? 'no error shown');
  await shot(page, '04-admin-conflict');
  await page.fill('input[name="path"]', '/cart');
  await savePathForm(page);
  error = await page.locator('.ui-error').first().textContent().catch(() => null);
  check('a reserved path (/cart) is refused with a Persian message', Boolean(error) && /[؀-ۿ]/.test(error), error ?? 'no error shown');
  eq('… and the product kept its path', (await api(`/catalog/products/${product.slug}`)).path, newPath);
  await page.fill('input[name="path"]', '/Bad Path/');
  await savePathForm(page);
  error = await page.locator('.ui-error').first().textContent().catch(() => null);
  check('a malformed path is refused with a Persian message', Boolean(error) && /[؀-ۿ]/.test(error), error ?? 'no error shown');

  // ---- 5. a category: change its path, filters keep working -------------------------------------------------------------
  const category = categories.find((c) => c.slug === 'lamps') ?? categories[0];
  const filterLink = await (async () => {
    await page.goto(`${BASE}${category.path}`);
    const href = await page.locator(`a[href^="${category.path}?"]`).first().getAttribute('href');
    return href;
  })();
  check('the category listing has filter links', Boolean(filterLink), filterLink ?? '');
  await page.goto(`${BASE}/admin/categories`);
  const detail = page.locator('details', { hasText: category.slug }).first();
  await detail.locator('summary').click();
  const newCategory = `/shop/smoke-${RUN}`;
  await detail.locator('input[name="path"]').fill(newCategory);
  await Promise.all([page.waitForLoadState('networkidle'), detail.locator('form:has(input[name="path"]) button[type="submit"]').first().click()]);
  await page.waitForTimeout(500);
  eq('category path saved', (await api('/catalog/categories')).flatMap((c) => [c, ...c.children]).find((c) => c.slug === category.slug).path, newCategory);
  const filterQuery = filterLink.slice(filterLink.indexOf('?'));
  const oldFiltered = await raw(context, `${category.path}${filterQuery}`);
  check('old category URL with filters 308-redirects keeping the filters', oldFiltered.status === 308 && oldFiltered.location === `${newCategory}${filterQuery}`, `${oldFiltered.status} ${oldFiltered.location}`);
  await page.goto(`${BASE}${category.path}${filterQuery}`);
  eq('the browser lands on the new category URL with the filter', new URL(page.url()).pathname + new URL(page.url()).search, `${newCategory}${filterQuery}`);
  const chips = await page.locator('main a, a').evaluateAll((links, base) => links.filter((a) => a.getAttribute('href')?.startsWith(`${base}?`)).length, newCategory);
  check('filter/sort links on the new category page are built on the new path', chips > 0, `${chips} links`);
  await page.goto(`${BASE}${newCategory}`);
  const firstFilter = await page.locator(`a[href^="${newCategory}?"]`).first().getAttribute('href');
  await page.goto(`${BASE}${firstFilter}`);
  check('clicking-through a filter on the new path works (200, products listed)', (await raw(context, firstFilter)).status === 200 && (await page.locator('.ui-product-grid a').count()) > 0, firstFilter);
  await shot(page, '05-category-filters-new-path');
  eq('category canonical (clean URL) is the new path', await (async () => { await page.goto(`${BASE}${newCategory}`); return canonicalOf(page); })(), `${BASE}${newCategory}`);

  // ---- 6. blog post: create with the default path, delete → 410 ---------------------------------------------------------
  const slug = `smoke-post-${RUN}`;
  await page.goto(`${BASE}/admin/blog/new`);
  await page.fill('input[name="title"]', 'نوشتهٔ آزمایشی');
  await page.fill('input[name="slug"]', slug);
  await page.fill('textarea[name="excerpt"]', 'خلاصهٔ آزمایشی');
  await page.fill('textarea[name="body"]', 'متن آزمایشی');
  await page.check('input[name="publish"]');
  await shot(page, '06-admin-blog-new');
  await axeCheck(page, '/admin/blog/new (path field)');
  await Promise.all([page.waitForURL(/\/admin\/blog\/\d+$/), page.click('form:has(input[name="path"]) button[type="submit"]')]);
  const postId = page.url().split('/').pop();
  eq('a new post is live at the default /blog/<slug> URL', (await raw(context, `/blog/${slug}`)).status, 200);
  await page.goto(`${BASE}/admin/blog/${postId}`);
  await page.fill('input[name="path"]', `/guides/${slug}`);
  await savePathForm(page);
  eq('post path changed: old URL is a 308', (await raw(context, `/blog/${slug}`)).status, 308);
  await page.goto(`${BASE}/admin/blog/${postId}`);
  await page.locator('details summary').last().click();
  await Promise.all([page.waitForURL(/\/admin\/blog$/), page.locator('details button[type="submit"]').click()]);
  const gone = await raw(context, `/guides/${slug}`);
  eq('a hard-deleted post answers 410 (current path)', gone.status, 410);
  eq('… and 410 on its old path', (await raw(context, `/blog/${slug}`)).status, 410);
  const goneBody = await context.request.get(`${BASE}/guides/${slug}`);
  check('… with the "gone" page text', (await goneBody.text()).includes('این صفحه دیگر وجود ندارد'), `status ${goneBody.status()}`);
  await page.goto(`${BASE}/guides/${slug}`);
  await shot(page, '07-gone-410');
  eq('an unknown path answers 404', (await raw(context, `/nothing-here-${RUN}`)).status, 404);
  eq('a draft/unknown entity under a known prefix answers 404', (await raw(context, `/product/does-not-exist-${RUN}`)).status, 404);
  eq('reserved static routes still win (/cart)', (await raw(context, '/cart')).status, 200);
  eq('/blog index still works', (await raw(context, '/blog')).status, 200);

  // ---- 7. redirects page: list, search, release, audit ------------------------------------------------------------------
  await page.goto(`${BASE}/admin/redirects`);
  await shot(page, '08-admin-redirects');
  const rows = await page.locator('tbody tr').count();
  check('the redirects page lists the old paths created above (product, category, the deleted post\'s two paths)', rows >= 4, `${rows} rows`);
  await page.goto(`${BASE}/admin/redirects?q=${encodeURIComponent(oldPath.split('/').pop())}`);
  check('searching by path finds the product\'s old path', (await page.locator('tbody tr', { hasText: oldPath }).count()) === 1);
  await axeCheck(page, '/admin/redirects (list, search, release, audit)');
  const oldRow = page.locator('tbody tr', { hasText: oldPath }).first();
  await oldRow.locator('summary').click();
  await Promise.all([page.waitForURL(/released=1/), oldRow.locator('button[type="submit"]').click()]);
  check('releasing shows the success message', (await page.locator('main, body').first().innerText()).includes('نشانی آزاد شد'));
  await shot(page, '09-admin-redirects-released');
  eq('a released old path is a 404 (no longer redirects)', (await raw(context, oldPath)).status, 404);
  check('the audit list shows the change', (await page.locator('section[aria-labelledby="route-audit"] li').count()) >= 4);

  // ---- 8. the info-page and category forms render the field ----------------------------------------------------------------
  await page.goto(`${BASE}/admin/content/about`);
  check('info-page form has the URL field', (await page.locator('input[name="path"]').count()) === 1);
  await axeCheck(page, '/admin/content/about (path field)');
  await page.goto(`${BASE}/admin/categories`);
  await axeCheck(page, '/admin/categories (path fields)');

  check('no uncaught browser errors', consoleErrors.length === 0, consoleErrors.slice(0, 3).join(' | '));
  await browser.close();

  const failed = results.filter((r) => !r.ok);
  fs.writeFileSync(path.join(OUT, 'results.json'), JSON.stringify({ run: RUN, base: BASE, results }, null, 2));
  console.log(`\n${results.length - failed.length}/${results.length} checks passed; screenshots in ${OUT}`);
  process.exit(failed.length === 0 ? 0 : 1);
})().catch((error) => {
  console.error(error);
  process.exit(2);
});
