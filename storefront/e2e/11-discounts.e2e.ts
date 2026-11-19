// Discount codes (F3, docs/21): an admin creates a fixed-amount code through the form, a guest shopper applies it in the
// cart, sees the discounted total through checkout, and the placed order keeps the snapshot.
import { expect, test } from './fixtures';
import { RUN, axeCheck, check, login, shot } from './helpers';

const code = `E2E${RUN}`.toUpperCase().replace(/[^A-Z0-9]/g, '').slice(0, 20);

test('discounts: admin creates a code, a shopper applies it, the order keeps the snapshot', async ({ page, browser }) => {
  await login(page);
  await page.goto('/admin/discounts');
  await axeCheck(page, '/admin/discounts');
  await shot(page, 'discounts-list');

  await page.goto('/admin/discounts/new');
  await axeCheck(page, '/admin/discounts/new');
  await page.fill('input[name="code"]', code);
  await page.selectOption('select[name="type"]', 'fixed');
  await page.fill('input[name="value"]', '10000');
  await Promise.all([page.waitForURL(/\/admin\/discounts\/\d+$/), page.locator('form button[type="submit"]').last().click()]);
  await expect(page.locator('h1')).toContainText(code);
  await axeCheck(page, '/admin/discounts/[id]');

  // A fresh, unauthenticated context: a real guest, not the admin session.
  const shopperContext = await browser.newContext();
  const shopperPage = await shopperContext.newPage();
  const products = await (await shopperContext.request.get('http://127.0.0.1:5167/api/v1/catalog/products?pageSize=60')).json();
  const product = products.result.items.find((p: any) => p.inStock);

  await shopperPage.goto(product.path);
  await shopperPage.getByRole('button', { name: 'افزودن به سبد' }).click();
  await shopperPage.waitForURL(/\/cart/);
  const subtotalText = await shopperPage.locator('body').innerText();
  check('cart has a discount-code field before any code is applied', subtotalText.includes('کد تخفیف'));

  await shopperPage.fill('input[name="code"]', code);
  await shopperPage.getByRole('button', { name: 'اعمال کد' }).click();
  await shopperPage.waitForLoadState('networkidle');
  const afterApply = await shopperPage.locator('body').innerText();
  check('the applied code shows in the cart summary', afterApply.includes(code));
  await shot(shopperPage, 'cart-with-discount');
  await axeCheck(shopperPage, '/cart with a discount applied');

  await shopperPage.getByRole('link', { name: 'ثبت سفارش' }).first().click();
  await shopperPage.waitForURL(/\/checkout/);
  await axeCheck(shopperPage, '/checkout with a discount applied');
  await shopperPage.fill('input[name="fullName"]', `خریدار تخفیف ${RUN}`);
  await shopperPage.fill('input[name="mobile"]', '09' + String(Date.now()).slice(-9));
  await shopperPage.fill('input[name="province"]', 'تهران');
  await shopperPage.fill('input[name="city"]', 'تهران');
  await shopperPage.fill('textarea[name="address"]', 'خیابان آزمایش، پلاک ۲');
  await shopperPage.fill('input[name="postalCode"]', '1234567890');
  await Promise.all([shopperPage.waitForURL(/\/order\//), shopperPage.locator('form button[type="submit"]').last().click()]);
  const orderText = await shopperPage.locator('main').innerText();
  check('the placed order shows the discount code and amount', orderText.includes(code) && orderText.includes('تخفیف'));
  await axeCheck(shopperPage, '/order/<token> with a discount applied');

  await shopperContext.close();
});
