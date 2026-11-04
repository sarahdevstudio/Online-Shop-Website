// Printable invoice and packing slip (docs/16): the content, no prices on the slip, the controls hidden when printing, and the
// paper size really used by the browser's PDF output (A4 / A5 from the @page rule).
import { expect, test } from './fixtures';
import { BASE, axeCheck, check, login, shot } from './helpers';

/** Width and height in points of the first page of a PDF produced by Chromium. */
function pageSize(pdf: Buffer): [number, number] {
  const match = /\/MediaBox\s*\[\s*0\s+0\s+([\d.]+)\s+([\d.]+)\s*\]/.exec(pdf.toString('latin1'));
  if (!match) throw new Error('no MediaBox in the PDF');
  return [Math.round(Number(match[1])), Math.round(Number(match[2]))];
}

test('invoice and packing slip of the newest order', async ({ page }) => {
  await login(page);
  await page.goto('/admin/orders');
  const href = await page.locator('tbody tr a').first().getAttribute('href');
  const id = /\/admin\/orders\/(\d+)/.exec(href ?? '')?.[1];
  expect(id, 'an order exists (the checkout spec places one)').toBeTruthy();

  // The order screen links to both pages.
  await page.goto(`/admin/orders/${id}`);
  await expect(page.getByRole('link', { name: 'چاپ فاکتور' })).toHaveAttribute('href', `/admin/print/${id}/invoice`);
  const number = (await page.locator('h1').first().innerText()).replace(/\D/g, '');

  // Invoice
  await page.goto(`/admin/print/${id}/invoice`);
  await expect(page.locator('h1')).toHaveText('فاکتور فروش');
  const sheet = page.locator('article');
  check('the invoice shows the order number', (await sheet.innerText()).replace(/[۰-۹]/g, (d) => String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d))).includes(number));
  check('the invoice shows a Jalali date (۱۴xx)', /۱۴[۰-۹]{2}\/[۰-۹]{2}\/[۰-۹]{2}/.test(await sheet.innerText()));
  check('the invoice shows prices and the total', (await sheet.innerText()).includes('جمع') && (await page.locator('thead th').allInnerTexts()).includes('قیمت واحد'));
  check('the invoice names the seller', (await sheet.locator('header p').innerText()).length > 3);
  await shot(page, 'invoice-a4');
  await axeCheck(page, 'invoice');
  expect(await page.locator('html').getAttribute('dir')).toBe('rtl');

  // Packing slip: no prices, a tick box per line
  await page.goto(`/admin/print/${id}/packing-slip?size=a5`);
  await expect(page.locator('h1')).toHaveText('برگهٔ بسته‌بندی');
  const headers = await page.locator('thead th').allInnerTexts();
  check('the packing slip has no price columns', !headers.includes('قیمت واحد') && !headers.includes('جمع'), headers.join('|'));
  check('the packing slip has a box per line', (await page.locator('tbody tr').count()) === (await page.locator('tbody .tick, tbody span[class*="tick"]').count()));
  await shot(page, 'slip-a5');
  await axeCheck(page, 'packing slip');

  // Print rendering: the controls are gone, and the paper size is the one asked for.
  await page.emulateMedia({ media: 'print' });
  expect(await page.locator('[class*="controls"]').isVisible(), 'controls are hidden when printing').toBe(false);
  const a5 = pageSize(await page.pdf({ preferCSSPageSize: true }));
  await page.emulateMedia({ media: 'screen' });
  await page.goto(`/admin/print/${id}/invoice?size=a4`);
  await page.emulateMedia({ media: 'print' });
  const a4 = pageSize(await page.pdf({ preferCSSPageSize: true }));
  check('A5 is 420 × 595 pt', a5[0] === 420 && a5[1] === 595, a5.join('×'));
  check('A4 is 595 × 842 pt', a4[0] === 595 && a4[1] === 842, a4.join('×'));

  // Anonymous visitors are sent to the login page.
  const anon = await page.context().browser()!.newContext({ baseURL: BASE });
  const response = await anon.request.get(`${BASE}/admin/print/${id}/invoice`, { maxRedirects: 0 });
  check('the print page is closed without a session', response.status() >= 300 && response.status() < 400, String(response.status()));
  await anon.close();
});
