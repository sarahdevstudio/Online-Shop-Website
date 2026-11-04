// The admin panel: the gate (no session → login), a wrong password, the order the checkout spec placed (list, detail, advance
// the status), and a product edit that shows on the storefront and is put back.
import { expect, test } from './fixtures';
import { ADMIN_PASS, ADMIN_USER, BASE, RUN, api, axeCheck, check, login, raw, shot } from './helpers';

test('the admin is closed without a session and refuses a wrong password', async ({ page, context }) => {
  const gate = await raw(context, '/admin/orders');
  check('no session: redirected to the login page', gate.status >= 300 && gate.status < 400 && (gate.location ?? '').includes('/admin/login'), `${gate.status} ${gate.location}`);
  const headers = (await context.request.get(`${BASE}/admin/login`)).headers();
  check('the admin is never indexed or cached', (headers['x-robots-tag'] ?? '').includes('noindex') && (headers['cache-control'] ?? '').includes('no-store'));

  await page.goto('/admin/login');
  await axeCheck(page, '/admin/login');
  await page.fill('input[name="username"]', ADMIN_USER);
  await page.fill('input[name="password"]', `${ADMIN_PASS}-wrong`);
  await page.click('form button[type="submit"]');
  await expect(page.locator('[role="alert"], .ui-error').first()).toBeVisible();
  expect(new URL(page.url()).pathname).toBe('/admin/login');
});

test('orders: list, detail, advance the status', async ({ page }) => {
  await login(page);
  await page.goto('/admin/orders');
  await shot(page, 'orders');
  await axeCheck(page, '/admin/orders');
  const row = page.locator('tbody tr', { hasText: `خریدار آزمایشی ${RUN}` }).first();
  // The checkout spec ran just before in the same run; running this file alone uses the newest order instead.
  const link = ((await row.count()) > 0 ? row : page.locator('tbody tr').first()).locator('a').first();
  await link.click();
  await page.waitForURL(/\/admin\/orders\/\d+$/);
  await shot(page, 'order-detail');
  await axeCheck(page, 'order detail');
  const advance = page.locator('form:has(input[name="status"]) button[type="submit"]').first();
  if ((await advance.count()) > 0) {
    await Promise.all([page.waitForLoadState('networkidle'), advance.click()]);
    await page.waitForTimeout(500);
    check('advancing shows no error', (await page.locator('.ui-error').count()) === 0);
  }
});

test('editing a product in the admin changes the storefront, and is undone', async ({ page }) => {
  await login(page);
  const product = (await api('/catalog/products?pageSize=60')).items.find((p: any) => p.inStock);
  const list = await page.request.get(`${BASE}/admin/products`);
  const id = new RegExp(`/admin/products/(\\d+)"[^>]*>[^<]*${product.name.slice(0, 6)}`).exec(await list.text())?.[1];
  expect(id, 'the product is in the admin list').toBeTruthy();

  const edited = `${product.name} ${RUN}`;
  await page.goto(`/admin/products/${id}`);
  await axeCheck(page, `/admin/products/${id}`);
  await page.fill('input[name="name"]', edited);
  await Promise.all([page.waitForLoadState('networkidle'), page.locator('form:has(input[name="name"]) button[type="submit"]').first().click()]);
  await page.waitForTimeout(600);
  await page.goto(product.path);
  await expect(page.locator('h1')).toContainText(RUN);

  await page.goto(`/admin/products/${id}`);
  await page.fill('input[name="name"]', product.name);
  await Promise.all([page.waitForLoadState('networkidle'), page.locator('form:has(input[name="name"]) button[type="submit"]').first().click()]);
  await page.waitForTimeout(600);
  await page.goto(product.path);
  await expect(page.locator('h1')).not.toContainText(RUN);
});
