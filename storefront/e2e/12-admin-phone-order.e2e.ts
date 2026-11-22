// G3: admin-created ("phone") orders. Search a variant by SKU, add it to the draft, fill the customer's details, place
// the order, and confirm the tracking link resolves to the real order page. axe on both states.
import { expect, test } from './fixtures';
import { api, axeCheck, check, login, shot } from './helpers';

test('admin creates a phone order: search, add, place, and the tracking link resolves', async ({ page }) => {
  await login(page);
  await page.goto('/admin/orders/new');
  await axeCheck(page, '/admin/orders/new');
  await shot(page, 'phone-order-empty');

  const products = (await api('/catalog/products?pageSize=1')) as { items: { slug: string }[] };
  const detail = (await api(`/catalog/products/${products.items[0].slug}`)) as { variants: { sku: string; price: number }[] };
  const sku = detail.variants[0].sku;

  await page.getByPlaceholder('نام محصول یا SKU را تایپ کنید…').fill(sku);
  await expect(page.getByText(sku, { exact: false })).toBeVisible();
  await page.getByText(sku, { exact: false }).first().click();
  await expect(page.locator('table')).toContainText(sku);

  await page.getByLabel('نام و نام خانوادگی').fill('مشتری آزمایشی');
  await page.getByLabel('موبایل مشتری').fill('09129876543');
  await page.getByLabel('استان').fill('تهران');
  await page.getByLabel('شهر').fill('تهران');
  await page.getByLabel('آدرس').fill('خیابان آزمایش، پلاک ۲');
  await page.getByLabel('کد پستی').fill('1234567890');
  await shot(page, 'phone-order-filled');

  await page.getByRole('button', { name: 'ثبت سفارش', exact: true }).click();
  await expect(page.getByText(/سفارش شماره/)).toBeVisible();
  await shot(page, 'phone-order-placed');
  await axeCheck(page, '/admin/orders/new (placed)');

  const link = await page.locator('code').innerText();
  check('the tracking link looks like an order URL', /\/order\/[\w-]{43}$/.test(link), link);

  const orderPage = await page.request.get(link);
  check('the tracking link resolves to the real order page', orderPage.status() === 200);
});
