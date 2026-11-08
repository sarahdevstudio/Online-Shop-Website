// Shopper features (docs/18): product comparison, the search combobox (keyboard, ARIA, typo tolerance) and reviews with moderation.
import { expect, test } from './fixtures';
import { BASE, RUN, api, axeCheck, check, login, shot } from './helpers';
import { validateJsonLd } from '../tests/helpers/jsonld-shapes';

test.describe.configure({ mode: 'serial' });

test('compare: up to four products, differences, sticky first column, noindex', async ({ page }) => {
  const products = (await api('/catalog/products?pageSize=60')).items as { slug: string; path: string; name: string }[];
  await page.goto('/compare');
  await expect(page.locator('h1')).toHaveText('مقایسهٔ محصولات');
  await expect(page.getByText('هنوز محصولی برای مقایسه')).toBeVisible();
  await axeCheck(page, '/compare (empty)');

  for (const product of products.slice(0, 4)) {
    await page.goto(product.path);
    await page.getByRole('button', { name: 'افزودن به مقایسه' }).click();
    await expect(page.getByRole('button', { name: 'حذف از مقایسه' })).toBeVisible(); // the action finished: the button now offers the opposite
  }
  await page.goto(products[4].path);
  await expect(page.getByText('مقایسه پر است')).toBeVisible(); // the fifth is refused
  await expect(page.getByRole('link', { name: /دیدن مقایسه/ })).toBeVisible();
  await expect(page.locator('header a[href="/compare"]')).toBeVisible();

  await page.goto('/compare');
  expect(await page.locator('meta[name="robots"]').getAttribute('content')).toContain('noindex');
  await expect(page.locator('thead th[scope="col"]')).toHaveCount(5); // attribute column + four products
  check('rows that differ are marked with text, not colour alone', (await page.locator('tbody tr th span', { hasText: 'متفاوت' }).count()) > 0);
  await shot(page, 'compare');
  await axeCheck(page, '/compare with four products');

  // the only-differences view drops the equal rows
  const all = await page.locator('tbody tr').count();
  await page.getByRole('link', { name: 'فقط تفاوت‌ها' }).click();
  expect(await page.locator('tbody tr').count()).toBeLessThanOrEqual(all);
  await axeCheck(page, '/compare?diff=1');

  // on a phone the table scrolls sideways and its first column stays in view
  await page.setViewportSize({ width: 375, height: 800 });
  await page.goto('/compare');
  const region = page.locator('[role="region"][tabindex="0"]');
  expect(await region.evaluate((el) => el.scrollWidth > el.clientWidth), 'the table is wider than the phone').toBe(true);
  expect(await page.locator('tbody th').first().evaluate((el) => getComputedStyle(el).position)).toBe('sticky');
  await page.setViewportSize({ width: 1280, height: 900 });

  // remove one, then clear
  await page.getByRole('button', { name: /^حذف .* از مقایسه$/ }).first().click();
  await expect(page.locator('thead th[scope="col"]')).toHaveCount(4);
  await page.getByRole('button', { name: 'پاک کردن مقایسه' }).click();
  await expect(page.getByText('هنوز محصولی برای مقایسه')).toBeVisible();
});

test('search: ARIA combobox, keyboard, typo tolerance and the results page', async ({ page }) => {
  await page.goto('/');
  const input = page.getByRole('combobox');
  await expect(input).toHaveAttribute('aria-expanded', 'false');
  await expect(input).toHaveAttribute('aria-autocomplete', 'list');

  await input.fill('لامپ');
  const listbox = page.getByRole('listbox');
  await expect(listbox).toBeVisible();
  await expect(input).toHaveAttribute('aria-expanded', 'true');
  expect(await page.getByRole('option').count()).toBeGreaterThan(1);
  await expect(page.getByRole('status').filter({ hasText: 'پیشنهاد' })).toBeVisible(); // announced to screen readers
  await shot(page, 'search-open');
  await axeCheck(page, 'home with the suggestion list open');

  // keyboard: Down selects the first option, aria-activedescendant follows, End jumps to "all results", Escape closes then clears
  await input.press('ArrowDown');
  const first = await input.getAttribute('aria-activedescendant');
  expect(first).toBeTruthy();
  await expect(page.locator(`[id="${first}"]`)).toHaveAttribute('aria-selected', 'true');
  await input.press('End');
  await expect(page.getByRole('option').last()).toHaveAttribute('aria-selected', 'true');
  await input.press('Home');
  await input.press('ArrowUp'); // wraps to the last
  await expect(page.getByRole('option').last()).toHaveAttribute('aria-selected', 'true');
  await input.press('Escape');
  await expect(input).toHaveAttribute('aria-expanded', 'false');
  await expect(input).toHaveValue('لامپ');
  await input.press('Escape');
  await expect(input).toHaveValue('');

  // Enter on an active option opens it
  await input.fill('لامپ');
  await expect(listbox).toBeVisible();
  await input.press('ArrowDown');
  const href = await page.locator('[role="option"][aria-selected="true"]').evaluate(() => document.activeElement?.getAttribute('aria-activedescendant'));
  expect(href).toBeTruthy();
  await Promise.all([page.waitForURL((url) => url.pathname !== '/'), input.press('Enter')]);
  expect(new URL(page.url()).pathname).not.toBe('/');

  // typo tolerance: a missing letter still suggests, and the results page says these are the nearest spellings
  await page.goto('/');
  await page.getByRole('combobox').fill('لمپ');
  await expect(page.getByRole('option').first()).toBeVisible();
  await expect(page.getByText('نزدیک‌ترین نتیجه‌ها')).toBeVisible();
  await page.getByRole('combobox').press('Enter');
  await page.waitForURL(/\/search\?q=/);
  await expect(page.getByRole('status').filter({ hasText: 'نتیجهٔ دقیقی پیدا نشد' })).toBeVisible();
  expect(await page.locator('.ui-product-grid a').count()).toBeGreaterThan(0);
  await axeCheck(page, '/search with nearest spellings');

  // nothing matches: the list says nothing is suggested and the page stays quiet
  await page.goto('/');
  await page.getByRole('combobox').fill('ققققققققق');
  await page.waitForTimeout(600);
  await expect(page.getByRole('combobox')).toHaveAttribute('aria-expanded', 'true'); // only the "search for ..." option
  expect(await page.getByRole('option').count()).toBe(1);
});

test('reviews: pending is invisible, approval shows it, the structured data appears only then', async ({ page, browser }) => {
  const product = (await api('/catalog/products?pageSize=60')).items.find((p: { inStock: boolean }) => p.inStock) as { slug: string; path: string; name: string };
  const mobile = `09${String(Math.floor(100_000_000 + Math.random() * 899_999_999))}`;
  await page.goto(product.path);
  const before = await page.locator('script[type="application/ld+json"]').allTextContents();
  check('no rating in the structured data before any approved review', !before.some((b) => b.includes('aggregateRating')));
  await expect(page.locator('section[aria-labelledby="reviews"]')).toContainText('هنوز نظری ثبت نشده');

  await page.getByText('نظر خود را بنویسید').click();
  await axeCheck(page, 'product page with the review form open');
  // an empty submit shows Persian errors next to the fields
  await page.getByRole('button', { name: 'ثبت نظر' }).click();
  await expect(page.locator('.ui-error').first()).toBeVisible();
  await page.fill('input[name="name"]', `خریدار ${RUN}`);
  await page.fill('input[name="mobile"]', mobile);
  await page.check('input[name="rating"][value="4"]');
  await page.fill('textarea[name="text"]', `کیفیت خوب بود، بسته‌بندی سالم. ${RUN}`);
  await page.getByRole('button', { name: 'ثبت نظر' }).click();
  await expect(page.getByText('پس از تأیید نمایش داده می‌شود. سپاسگزاریم')).toBeVisible();
  await expect(page.locator('section[aria-labelledby="reviews"]')).not.toContainText(RUN); // held for moderation

  // the same number cannot review the same product twice
  await page.fill('input[name="name"]', 'دوباره');
  await page.fill('input[name="mobile"]', mobile);
  await page.check('input[name="rating"][value="5"]');
  await page.fill('textarea[name="text"]', 'یک نظر دوم از همان شماره، که باید رد شود.');
  await page.getByRole('button', { name: 'ثبت نظر' }).click();
  await expect(page.locator('main')).toContainText('قبلاً نظر ثبت شده');

  // moderation
  await login(page);
  await page.goto('/admin/reviews');
  await axeCheck(page, '/admin/reviews');
  const row = page.locator('tbody tr', { hasText: RUN }).first();
  await expect(row).toContainText(mobile); // the number is visible to staff only
  await shot(page, 'reviews-queue');
  await Promise.all([page.waitForLoadState('networkidle'), row.getByRole('button', { name: 'تأیید' }).click()]);
  // the decision moves the review to the other tab (and out of this one)
  await expect(page.locator('tbody tr', { hasText: RUN })).toHaveCount(0);
  await page.goto('/admin/reviews?status=approved');
  await expect(page.locator('tbody tr', { hasText: RUN }).first()).toContainText('تأییدشده');

  // approved: public, counted, in the structured data (valid shapes), without the number
  const anon = await browser.newContext({ baseURL: BASE, locale: 'fa-IR' });
  const visitor = await anon.newPage();
  await visitor.goto(product.path);
  const section = visitor.locator('section[aria-labelledby="reviews"]');
  await expect(section).toContainText(RUN);
  await expect(section).toContainText('۴ از ۵');
  expect(await section.innerText()).not.toContain(mobile);
  const docs = (await visitor.locator('script[type="application/ld+json"]').allTextContents()).map((b) => JSON.parse(b));
  const productDoc = docs.find((d) => d['@type'] === 'Product');
  expect(productDoc.aggregateRating).toMatchObject({ ratingValue: 4, reviewCount: 1 });
  expect(productDoc.review).toHaveLength(1);
  expect(validateJsonLd(productDoc)).toEqual([]);
  await axeCheck(visitor, 'product page with an approved review');
  await anon.close();

  // rejected again: gone from the page and from the structured data
  await page.goto('/admin/reviews?status=approved');
  await Promise.all([page.waitForLoadState('networkidle'), page.locator('tbody tr', { hasText: RUN }).first().getByRole('button', { name: 'رد' }).click()]);
  await expect(page.locator('tbody tr', { hasText: RUN })).toHaveCount(0);
  await page.goto('/admin/reviews?status=rejected');
  await expect(page.locator('tbody tr', { hasText: RUN }).first()).toContainText('ردشده');
  await page.goto(product.path);
  await expect(page.locator('section[aria-labelledby="reviews"]')).not.toContainText(RUN);
  expect((await page.locator('script[type="application/ld+json"]').allTextContents()).some((b) => b.includes('aggregateRating'))).toBe(false);
});
