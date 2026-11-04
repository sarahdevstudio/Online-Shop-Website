// Product export and import in the browser (docs/16): download the table, upload a file with an error (the per-row Persian message
// shows and nothing can be applied), fix it, preview, apply, and the product is on the shop. axe on every state.
import { writeFileSync, mkdirSync } from 'node:fs';
import { expect, test } from './fixtures';
import { RUN, api, axeCheck, check, login, shot } from './helpers';

const dir = 'e2e-results/transfer';
const slug = `import-${RUN}`;
const header = 'product_slug,product_name,category,brand,status,description,path,sku,price,compare_at_price,stock,variant_active,sort_order,image_ids';

test('export, a failing import, a fixed import', async ({ page }) => {
  mkdirSync(dir, { recursive: true });
  await login(page);
  await page.goto('/admin/products');
  await page.getByRole('link', { name: 'ورود و خروج (CSV / اکسل)' }).click();
  await page.waitForURL(/\/admin\/products\/transfer$/);
  await axeCheck(page, '/admin/products/transfer');
  await shot(page, 'transfer-empty');

  // Export: both formats download with a sensible name and content.
  const [csv] = await Promise.all([page.waitForEvent('download'), page.getByRole('link', { name: 'دریافت CSV' }).click()]);
  expect(csv.suggestedFilename()).toMatch(/^products-\d{8}-\d{4}\.csv$/);
  const csvPath = `${dir}/export.csv`;
  await csv.saveAs(csvPath);
  const [xlsx] = await Promise.all([page.waitForEvent('download'), page.getByRole('link', { name: 'دریافت اکسل (xlsx)' }).click()]);
  expect(xlsx.suggestedFilename()).toMatch(/\.xlsx$/);

  const categories = (await api('/catalog/categories')) as { path: string }[];
  const category = categories[0].path;

  // A file with a bad row: the preview shows the Persian message and offers no apply.
  const bad = `${header}\r\n${slug},لامپ واردشده,${category},,published,توضیح,,${slug}-1,abc,,5,1,0,\r\n`;
  writeFileSync(`${dir}/bad.csv`, '﻿' + bad);
  await page.setInputFiles('input[type="file"]', `${dir}/bad.csv`);
  await page.getByRole('button', { name: /پیش‌نمایش/ }).click();
  await expect(page.getByText('فایل خطا دارد')).toBeVisible();
  const row = page.locator('tbody tr', { hasText: 'price' });
  await expect(row).toContainText('قیمت باید عدد صحیح');
  check('no apply button while there are errors', (await page.getByRole('button', { name: 'اعمال تغییرات' }).count()) === 0);
  await shot(page, 'transfer-errors');
  await axeCheck(page, 'transfer with errors');

  // The fixed file: preview says what will happen, apply does it.
  const good = `${header}\r\n${slug},لامپ واردشده,${category},,published,توضیح,,${slug}-1,۱۲۳٬۰۰۰,,5,1,0,\r\n${slug},,,,,,,${slug}-2,150000,,3,1,1,\r\n`;
  writeFileSync(`${dir}/good.csv`, '﻿' + good);
  await page.setInputFiles('input[type="file"]', `${dir}/good.csv`);
  await page.getByRole('button', { name: /پیش‌نمایش/ }).click();
  await expect(page.getByText('فایل مشکلی ندارد')).toBeVisible();
  await expect(page.locator('ul li', { hasText: 'محصول جدید' })).toContainText('۱');
  await shot(page, 'transfer-preview');
  await axeCheck(page, 'transfer preview');
  expect((await api(`/catalog/products/${slug}`)) ?? null, 'a dry run created nothing').toBeNull();

  await page.getByRole('button', { name: 'اعمال تغییرات' }).click();
  await expect(page.getByText('تغییرات اعمال شد')).toBeVisible();
  await shot(page, 'transfer-applied');
  await axeCheck(page, 'transfer applied');

  const created = await api(`/catalog/products/${slug}`);
  expect(created.name).toBe('لامپ واردشده');
  expect(created.variants.map((v: { price: number }) => v.price).sort()).toEqual([123000, 150000]);
  await page.goto(created.path ?? `/product/${slug}`);
  await expect(page.locator('h1')).toContainText('لامپ واردشده');
});
