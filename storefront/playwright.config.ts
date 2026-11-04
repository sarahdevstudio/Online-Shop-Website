import { defineConfig } from '@playwright/test';

/**
 * Browser end-to-end suite (docs/15-e2e.md). It drives a RUNNING stack: the API in Development (sample catalog, SQL Server) and
 * the production-built storefront. CI starts both (`.github/workflows/ci.yml`, job `e2e`); locally see the header of e2e/helpers.ts.
 * The specs share one database and change data (paths, a blog post, an order), so they run one after the other, in file order.
 */
export default defineConfig({
  testDir: './e2e',
  globalSetup: './e2e/global-setup.ts',
  testMatch: '**/*.e2e.ts',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 90_000,
  expect: { timeout: 10_000 },
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  outputDir: 'e2e-results',
  use: {
    baseURL: process.env.BASE ?? 'http://localhost:3000',
    locale: 'fa-IR',
    viewport: { width: 1280, height: 900 },
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
  },
});
