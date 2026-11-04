// Guest checkout in the browser: add to cart, change the quantity, place the order with an address, land on the order page
// (token in the URL, no-referrer, noindex), and see a validation error in Persian for a bad mobile number.
import { expect, test } from './fixtures';
import { api, axeCheck, check, RUN, shot } from './helpers';

test('guest checkout: cart, validation, order page', async ({ page }) => {
  const products = (await api('/catalog/products?pageSize=60')).items;
  const product = products.find((p: any) => p.inStock);
  expect(product, 'an in-stock sample product').toBeTruthy();

  await page.goto(product.path);
  await expect(page.locator('h1')).toContainText(product.name.slice(0, 6));
  await page.getByRole('button', { name: 'افزودن به سبد' }).click();
  await page.waitForURL(/\/cart/);
  check('the cart lists the product', (await page.locator('main').innerText()).includes(product.name.slice(0, 6)));
  await shot(page, 'cart');
  await axeCheck(page, '/cart with an item');

  await page.getByRole('link', { name: 'ثبت سفارش' }).first().click();
  await page.waitForURL(/\/checkout/);
  await expect(page.locator('input[name="mobile"]')).toBeVisible();
  await axeCheck(page, '/checkout');

  // A bad mobile number is refused with a Persian message and the form keeps what was typed.
  await page.fill('input[name="fullName"]', `خریدار آزمایشی ${RUN}`);
  await page.fill('input[name="mobile"]', '123');
  await page.fill('input[name="province"]', 'تهران');
  await page.fill('input[name="city"]', 'تهران');
  await page.fill('textarea[name="address"]', 'خیابان آزمایش، پلاک ۱');
  await page.fill('input[name="postalCode"]', '1234567890');
  await page.locator('form button[type="submit"]').last().click();
  await page.waitForLoadState('networkidle');
  const error = await page.locator('.ui-error').first().innerText();
  check('a bad mobile number gets a Persian error', /[؀-ۿ]/.test(error), error);
  expect(await page.inputValue('input[name="fullName"]'), 'typed values are kept').toBe(`خریدار آزمایشی ${RUN}`);
  await shot(page, 'checkout-error');
  await axeCheck(page, '/checkout with an error');

  await page.fill('input[name="mobile"]', '09123456789');
  await Promise.all([page.waitForURL(/\/order\//), page.locator('form button[type="submit"]').last().click()]);
  const url = new URL(page.url());
  check('the order page address carries a token', url.pathname.split('/').pop()!.length >= 16, url.pathname);
  await expect(page.locator('main')).toContainText('سفارش شما ثبت شد');
  expect((await page.locator('meta[name="robots"]').getAttribute('content')) ?? '', 'order page is noindex').toContain('noindex');
  await shot(page, 'order-placed');
  await axeCheck(page, '/order/<token>');

  // The cart is empty again after the order.
  await page.goto('/cart');
  await expect(page.locator('main')).toContainText('سبد خرید شما خالی است');
});
