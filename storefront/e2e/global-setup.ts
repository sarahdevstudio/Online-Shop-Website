import { chromium } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { ADMIN_PASS, ADMIN_USER, AUTH_FILE, BASE } from './helpers';

/** Signs in to the admin once; the specs reuse the session (the login form is throttled, and one sign-in per run is realistic). */
export default async function globalSetup() {
  mkdirSync('.e2e-auth', { recursive: true });
  const browser = await chromium.launch();
  const page = await browser.newPage({ baseURL: BASE });
  await page.goto('/admin/login');
  await page.fill('input[name="username"]', ADMIN_USER);
  await page.fill('input[name="password"]', ADMIN_PASS);
  await Promise.all([page.waitForURL(/\/admin(\/)?$/), page.click('form button[type="submit"]')]);
  await page.context().storageState({ path: AUTH_FILE });
  await browser.close();
}
