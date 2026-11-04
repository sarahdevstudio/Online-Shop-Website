// Shared helpers for the end-to-end specs. The stack must already be running:
//
//   API=http://127.0.0.1:5167 BASE=http://localhost:3000 ADMIN_USER=… ADMIN_PASS=… npx playwright test
//
// API and storefront are the ones started by CI (see .github/workflows/ci.yml, job `e2e`), or any local Development API with the
// sample catalog plus `npm run build && npm start`.
import { readFileSync } from 'node:fs';
import { expect, test, type BrowserContext, type Page } from '@playwright/test';

const axeSource = readFileSync(require.resolve('axe-core/axe.min.js'), 'utf8');

export const BASE = (process.env.BASE ?? 'http://localhost:3000').replace(/\/$/, '');
export const API = (process.env.API ?? 'http://127.0.0.1:5167').replace(/\/$/, '');
export const ADMIN_USER = process.env.ADMIN_USER ?? 'smoke-admin';
export const ADMIN_PASS = process.env.ADMIN_PASS ?? 'Smoke-Admin-Pass-2026!';
export const AUTH_FILE = '.e2e-auth/admin.json';
/** Distinguishes this run's data from earlier runs in the same database. */
export const RUN = Date.now().toString(36);

/** Like the old smoke scripts: a failed check is recorded and the test goes on, then fails at the end. */
export const check = (name: string, ok: boolean, detail = '') => expect.soft(ok, `${name}${detail ? ` — ${detail}` : ''}`).toBe(true);
export const eq = <T>(name: string, actual: T, expected: T) => expect.soft(actual, name).toBe(expected);

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export async function api(pathname: string): Promise<any> {
  const response = await fetch(`${API}/api/v1${pathname}`);
  return (await response.json()).result;
}

/** Status and Location of the first response, without following redirects. */
export async function raw(context: BrowserContext, url: string) {
  const response = await context.request.get(`${BASE}${url}`, { maxRedirects: 0 });
  return { status: response.status(), location: response.headers().location ?? null, body: await response.text() };
}

/** Gives the page's browser context the admin session that e2e/global-setup.ts created, then opens the admin home. */
export async function login(page: Page) {
  const state = JSON.parse(readFileSync(AUTH_FILE, 'utf8'));
  await page.context().addCookies(state.cookies);
  await page.goto('/admin');
  await expect(page.locator('h1').first()).toBeVisible();
  expect(new URL(page.url()).pathname, 'signed in').not.toBe('/admin/login');
}

/** WCAG 2.2 A/AA plus best practices on the page as it is now. Fails on any violation (serious or not). */
export async function axeCheck(page: Page, name: string) {
  await page.evaluate(axeSource);
  const violations = await page.evaluate(async () => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const axe = (window as any).axe;
    const result = await axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa', 'best-practice'] } });
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return result.violations.map((v: any) => `${v.id} (${v.nodes.length})`);
  });
  check(`axe: ${name}`, violations.length === 0, violations.join(', '));
}

export const hrefs = (page: Page, selector: string) => page.locator(selector).evaluateAll((links) => links.map((a) => a.getAttribute('href')));

export async function canonicalOf(page: Page) {
  return page.locator('link[rel="canonical"]').first().getAttribute('href').catch(() => null);
}

export async function jsonLd(page: Page) {
  return (await page.locator('script[type="application/ld+json"]').allTextContents()).join('\n');
}

/** Attaches a full-page screenshot to the test report (HTML report in CI). */
export async function shot(page: Page, name: string) {
  await test.info().attach(name, { body: await page.screenshot({ fullPage: true }), contentType: 'image/png' });
}

/** Saves the form that contains the path field and waits until the server action finished. */
export async function savePathForm(page: Page) {
  const submit = page.locator('form:has(input[name="path"]) button[type="submit"]').first();
  await Promise.all([page.waitForLoadState('networkidle'), submit.click()]);
  await page.waitForTimeout(400);
}
