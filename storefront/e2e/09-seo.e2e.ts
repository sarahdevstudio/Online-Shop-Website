// SEO managed from the admin (docs/17): per-entity overrides, noindex (and the sitemap), FAQ with FAQPage, the category guide on
// the clean URL only, the split sitemaps with their index, BreadcrumbList and JSON-LD shapes on every entity page, and the Search
// Console / GA4 tags (rendered only when configured; the GA4 script starts after the first interaction).
import { expect, test } from './fixtures';
import { BASE, RUN, api, axeCheck, check, login, shot } from './helpers';
import { validateJsonLd } from '../tests/helpers/jsonld-shapes';
import type { Locator, Page } from '@playwright/test';

async function jsonLd(page: Page): Promise<Record<string, unknown>[]> {
  const blocks = await page.locator('script[type="application/ld+json"]').allTextContents();
  return blocks.map((b) => JSON.parse(b));
}

const byType = (docs: Record<string, unknown>[], type: string) => docs.filter((d) => d['@type'] === type);

/** The SEO panel of the page you are on: fill and save it, wait for the saved banner. */
async function saveSeoPanel(page: Page, fill: () => Promise<void>, root: Page | Locator = page) {
  const form = root.locator('form:has(input[name="metaTitle"])');
  await fill();
  await Promise.all([page.waitForLoadState('networkidle'), form.locator('button[type="submit"]').click()]);
  await expect(root.locator('form:has(input[name="metaTitle"])')).toContainText('تنظیمات سئو ذخیره شد');
}

test.describe.configure({ mode: 'serial' });

test('a product: overrides, noindex, FAQ and structured data', async ({ page }) => {
  await login(page);
  const product = (await api('/catalog/products?pageSize=60')).items.find((p: { inStock: boolean }) => p.inStock);
  const list = await page.request.get(`${BASE}/admin/products`);
  const id = new RegExp(`/admin/products/(\\d+)"[^>]*>[^<]*${product.name.slice(0, 6)}`).exec(await list.text())?.[1];
  await page.goto(`/admin/products/${id}`);
  await axeCheck(page, 'product edit with the SEO panel');
  const title = `عنوان سئو ${RUN}`;
  await saveSeoPanel(page, async () => {
    await page.fill('input[name="metaTitle"]', title);
    await page.fill('textarea[name="metaDescription"]', `توضیح سئو ${RUN}`);
    await expect(page.getByText(/[۰-۹]+ از حدود ۶۰ نویسه/)).toBeVisible(); // the live length hint
    await page.getByRole('button', { name: 'افزودن پرسش' }).click();
    await page.fill('input[name="faqQuestion"]', 'ضمانت دارد؟');
    await page.fill('textarea[name="faqAnswer"]', 'بله، دوازده ماه ضمانت.');
  });
  await shot(page, 'seo-panel-saved');

  await page.goto(product.path);
  await expect(page).toHaveTitle(new RegExp(title));
  expect(await page.locator('meta[name="description"]').getAttribute('content')).toBe(`توضیح سئو ${RUN}`);
  expect(await page.locator('meta[name="robots"]').count(), 'indexable while noindex is off').toBe(0);
  await expect(page.locator('section[aria-labelledby="faq"]')).toContainText('ضمانت دارد؟');
  const docs = await jsonLd(page);
  for (const doc of docs) expect(validateJsonLd(doc), JSON.stringify(doc['@type'])).toEqual([]);
  check('product page: one Product, one FAQPage, one BreadcrumbList', byType(docs, 'Product').length === 1 && byType(docs, 'FAQPage').length === 1 && byType(docs, 'BreadcrumbList').length === 1, docs.map((d) => d['@type']).join(','));
  await axeCheck(page, 'product page with FAQ');

  // noindex: robots meta, and out of the product sitemap
  await page.goto(`/admin/products/${id}`);
  await saveSeoPanel(page, async () => {
    await page.check('input[name="noIndex"]');
    await expect(page.getByText('از گوگل و نقشهٔ سایت بیرون می‌رود')).toBeVisible(); // the warning
  });
  await page.goto(product.path);
  expect(await page.locator('meta[name="robots"]').getAttribute('content')).toContain('noindex');
  const sitemap = await (await page.request.get(`${BASE}/sitemaps/products.xml`)).text();
  check('a noindex product is not in the sitemap', !sitemap.includes(`${product.path}</loc>`));
  const others = (await api('/catalog/products?pageSize=60')).items.filter((p: { slug: string }) => p.slug !== product.slug);
  check('the other products are', others.every((p: { path: string }) => sitemap.includes(`${p.path}</loc>`)) || others.length === 0);

  // back to indexable, SEO fields cleared
  await page.goto(`/admin/products/${id}`);
  await saveSeoPanel(page, async () => {
    await page.uncheck('input[name="noIndex"]');
    await page.fill('input[name="metaTitle"]', '');
    await page.fill('textarea[name="metaDescription"]', '');
    await page.getByRole('button', { name: 'حذف این پرسش' }).click();
  });
  await page.goto(product.path);
  await expect(page).not.toHaveTitle(new RegExp(title));
  expect(await page.locator('#faq').count()).toBe(0);
});

test('a category: guide and FAQ on the clean URL only', async ({ page }) => {
  await login(page);
  const category = ((await api('/catalog/categories')) as { slug: string; path: string; name: string }[]).find((c) => c.slug === 'lamps') ?? (await api('/catalog/categories'))[0];
  await page.goto('/admin/categories');
  const panel = page.locator('details', { hasText: category.slug }).first();
  await panel.locator('summary').first().click();
  await saveSeoPanel(page, async () => {
    await panel.locator('textarea[name="intro"]').fill(`## راهنمای خرید ${RUN}\n\nتوان را با اندازهٔ اتاق بسنجید.`);
    await panel.getByRole('button', { name: 'افزودن پرسش' }).click();
    await panel.locator('input[name="faqQuestion"]').fill('چه توانی بخرم؟');
    await panel.locator('textarea[name="faqAnswer"]').fill('برای اتاق ۱۲ متری حدود ۹ وات.');
  }, panel);

  await page.goto(category.path);
  await expect(page.locator('#guide')).toHaveText('راهنمای خرید');
  await expect(page.locator('.ui-prose h2')).toContainText(`راهنمای خرید ${RUN}`);
  await expect(page.locator('#faq')).toBeVisible();
  const docs = await jsonLd(page);
  for (const doc of docs) expect(validateJsonLd(doc)).toEqual([]);
  check('category page: one FAQPage and one BreadcrumbList', byType(docs, 'FAQPage').length === 1 && byType(docs, 'BreadcrumbList').length === 1);
  await axeCheck(page, 'category page with guide and FAQ');
  await shot(page, 'category-guide');

  // a sorted or filtered address is not the category's own address: no guide, no FAQ
  await page.goto(`${category.path}?sort=price-asc`);
  expect(await page.locator('#guide').count(), 'no guide on a sorted listing').toBe(0);
  expect(await page.locator('#faq').count()).toBe(0);
  const filterHref = await page.locator(`a[href^="${category.path}?"]`).first().getAttribute('href');
  if (filterHref) {
    await page.goto(filterHref);
    expect(await page.locator('#guide').count(), 'no guide on a filtered listing').toBe(0);
  }

  await page.goto('/admin/categories');
  const again = page.locator('details', { hasText: category.slug }).first();
  await again.locator('summary').first().click();
  await saveSeoPanel(page, async () => {
    await again.locator('textarea[name="intro"]').fill('');
    await again.getByRole('button', { name: 'حذف این پرسش' }).click();
  }, again);
});

test('every entity page has exactly one valid BreadcrumbList and valid structured data', async ({ page }) => {
  const product = (await api('/catalog/products?pageSize=1')).items[0];
  const categories = (await api('/catalog/categories')) as { path: string }[];
  const pages = (await api('/catalog/pages')) as { path: string }[];
  const urls = [categories[0].path, product.path, '/blog/perf-guide', pages[0].path, '/blog', '/'];
  for (const url of urls) {
    await page.goto(url);
    const docs = await jsonLd(page);
    expect(docs.length, `${url} has structured data`).toBeGreaterThan(0);
    for (const doc of docs) expect(validateJsonLd(doc, url), `${url} ${String(doc['@type'])}`).toEqual([]);
    if (url !== '/' && url !== '/blog') check(`${url}: exactly one BreadcrumbList`, byType(docs, 'BreadcrumbList').length === 1, String(byType(docs, 'BreadcrumbList').length));
  }
  await page.goto('/blog/perf-guide');
  check('a blog post has a BlogPosting', byType(await jsonLd(page), 'BlogPosting').length === 1);
  await page.goto('/');
  const home = await jsonLd(page);
  check('home: Organization and WebSite', byType(home, 'Organization').length >= 1 && byType(home, 'WebSite').length === 1);
});

test('the sitemap index and the four sitemaps', async ({ request }) => {
  const index = await (await request.get(`${BASE}/sitemap.xml`)).text();
  expect(index).toContain('<sitemapindex');
  const locs = [...index.matchAll(/<loc>([^<]+)<\/loc>/g)].map((m) => m[1]);
  check('the index lists the child sitemaps', ['products', 'categories', 'pages', 'blog'].every((name) => locs.some((l) => l.endsWith(`/sitemaps/${name}.xml`))), locs.join(' '));
  expect(index).toMatch(/<lastmod>\d{4}-\d{2}-\d{2}T/);
  for (const loc of locs) {
    const response = await request.get(loc.replace(/^https?:\/\/[^/]+/, BASE));
    expect(response.status(), loc).toBe(200);
    expect(response.headers()['content-type']).toContain('xml');
    const xml = await response.text();
    expect(xml).toContain('<urlset');
    expect([...xml.matchAll(/<loc>([^<]+)<\/loc>/g)].every((m) => /^https?:\/\//.test(m[1]))).toBe(true);
  }
  expect((await request.get(`${BASE}/sitemaps/nope.xml`)).status()).toBe(404);
});

test('Search Console tag and GA4: rendered only when set; GA4 starts after the first interaction', async ({ page, context }) => {
  await login(page);
  const token = 'abcDEF1234567890_-abcDEF12';
  const ga4 = 'G-E2ETEST12';
  const requested: string[] = [];
  await context.route(/googletagmanager\.com/, (route) => {
    requested.push(route.request().url());
    return route.fulfill({ status: 200, contentType: 'application/javascript', body: 'window.__gtagLoaded = true;' });
  });

  async function publishSeoSite(values: { token: string; ga4: string }) {
    await page.goto('/admin/site/seo');
    await page.fill('input[name="searchConsoleToken"]', values.token);
    await page.fill('input[name="ga4Id"]', values.ga4);
    await Promise.all([page.waitForLoadState('networkidle'), page.click('button[name="intent"][value="publish"]')]);
    await page.waitForTimeout(800);
  }

  // Bad values are refused in Persian next to the field.
  await page.goto('/admin/site/seo');
  await axeCheck(page, '/admin/site/seo with the analytics fields');
  await page.fill('input[name="ga4Id"]', 'UA-1-1');
  await page.click('button[name="intent"][value="publish"]');
  await expect(page.locator('.ui-error').first()).toContainText('G-');

  await publishSeoSite({ token, ga4 });
  // The proxy remembers "no analytics" for up to 30 s: wait until the policy names Google Tag Manager.
  await expect
    .poll(async () => ((await page.request.get(`${BASE}/`)).headers()['content-security-policy'] ?? '').includes('googletagmanager.com'), { timeout: 60_000, intervals: [2_000] })
    .toBe(true);

  await page.goto('/');
  expect(await page.locator('meta[name="google-site-verification"]').getAttribute('content')).toBe(token);
  await page.waitForTimeout(2000); // well inside the quiet time after load
  expect(requested, 'nothing is requested before the first interaction').toEqual([]);
  await page.mouse.move(100, 100);
  await page.keyboard.press('Tab');
  await expect.poll(() => requested.length, { timeout: 10_000 }).toBeGreaterThan(0);
  expect(requested[0]).toContain(`id=${ga4}`);
  await expect.poll(() => page.evaluate(() => (window as unknown as { __gtagLoaded?: boolean }).__gtagLoaded === true)).toBe(true);
  check('the admin never gets the analytics policy or the tag', !((await page.request.get(`${BASE}/admin/login`)).headers()['content-security-policy'] ?? '').includes('googletagmanager'));

  // Cleared again: no tag, no script.
  await publishSeoSite({ token: '', ga4: '' });
  await page.goto('/');
  expect(await page.locator('meta[name="google-site-verification"]').count()).toBe(0);
});
