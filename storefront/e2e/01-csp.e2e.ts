// Content-Security-Policy (docs/15): every main shop and admin page loads with no violation, no uncaught error and no failed
// asset; the response carries a per-request nonce, every executable inline script has it, and axe finds nothing.
// (e2e/fixtures.ts fails the test on any violation the browser reports, in every spec.)
import { expect, test } from './fixtures';
import { BASE, api, axeCheck, check, login } from './helpers';

async function openChecked(page: import('@playwright/test').Page, url: string) {
  const response = await page.goto(url);
  expect(response?.status(), `${url} status`).toBe(200);
  const csp = response?.headers()['content-security-policy'] ?? '';
  const nonce = /'nonce-([^']+)'/.exec(csp)?.[1];
  check(`${url}: policy with a nonce, no unsafe-eval, no inline-script`, Boolean(nonce) && !csp.includes('unsafe-eval') && !/script-src[^;]*unsafe-inline/.test(csp), csp.slice(0, 160));
  expect(response?.headers()['permissions-policy'], `${url} Permissions-Policy`).toContain('camera=()');
  await page.waitForLoadState('networkidle');
  // The page really rendered (not an error shell): a heading, and a document that was hydrated by Next's nonce-tagged scripts.
  expect(await page.locator('h1').first().innerText(), `${url} has a heading`).not.toBe('');
  const scripts = await page.locator('script:not([type="application/ld+json"])').evaluateAll((all) => all.map((s) => ({ nonce: s.getAttribute('nonce') || (s as HTMLScriptElement).nonce, src: s.getAttribute('src'), inline: !s.getAttribute('src') })));
  const untagged = scripts.filter((s) => s.inline && !s.nonce);
  check(`${url}: every inline script carries the nonce`, untagged.length === 0, `${untagged.length} of ${scripts.length} without`);
  check(`${url}: the nonce is the one in the header`, scripts.filter((s) => s.inline).every((s) => s.nonce === nonce));
  await axeCheck(page, url);
}

test('public shop pages', async ({ page }) => {
  const categories = (await api('/catalog/categories')).flatMap((c: any) => [c, ...c.children]);
  const products = (await api('/catalog/products?pageSize=5')).items;
  const urls = ['/', categories[0].path, `${categories[0].path}?sort=price-asc`, products[0].path, '/cart', '/blog', '/search?q=led', '/account'];
  for (const url of urls) await openChecked(page, url);
});

test('a blog post (seeded by scripts/perf/seed.mjs) and an info page', async ({ page }) => {
  await openChecked(page, '/blog/perf-guide');
  const pages = await api('/catalog/pages');
  await openChecked(page, pages[0].path);
});

test('the admin panel pages', async ({ page }) => {
  await login(page);
  const urls = ['/admin', '/admin/account', '/admin/products', '/admin/categories', '/admin/brands', '/admin/orders', '/admin/blog', '/admin/content', '/admin/redirects', '/admin/settings', '/admin/translations', '/admin/site', '/admin/site/home', '/admin/site/header-footer', '/admin/site/theme', '/admin/site/seo', '/admin/site/badges', '/admin/site/audit'];
  for (const url of urls) await openChecked(page, url);
});

test.describe('enforcement', () => {
  test.use({ provokesCspViolation: true });
  // Playwright's own evaluate() bypasses the page policy, so this uses a real navigation: the home page, as the server sends it
  // (with its real headers), plus one inline script that has no nonce. The browser must refuse to run it.
  test('an inline script without the nonce is blocked (the policy is enforced, not report-only)', async ({ page }) => {
    await page.route('**/csp-probe', async (route) => {
      const real = await route.fetch({ url: `${BASE}/` });
      const body = (await real.text()).replace('</head>', '<script>window.__ran = true</script></head>');
      await route.fulfill({ response: real, body });
    });
    const refused: string[] = [];
    page.on('console', (message) => {
      if (/inline script violates the following Content Security Policy/i.test(message.text())) refused.push(message.text());
    });
    await page.goto(`${BASE}/csp-probe`);
    await page.waitForLoadState('networkidle');
    expect(refused.length, 'the browser refused the inline script').toBe(1);
    expect(await page.evaluate(() => (window as unknown as { __ran?: boolean }).__ran)).toBeUndefined();
    // Next's own nonce-tagged scripts still ran: the page is interactive.
    expect(await page.locator('h1').first().innerText()).not.toBe('');
  });
});
