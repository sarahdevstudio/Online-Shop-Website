import { expect, test as base } from '@playwright/test';

/**
 * Every page of every test is watched: a Content-Security-Policy violation (reported by the browser as an event and as a console
 * error), an uncaught exception or a failed same-origin script/style/font/image request fails the test, whichever flow loaded it.
 */
/** `test.use({ provokesCspViolation: true })` for the one test that triggers a violation on purpose. */
export const test = base.extend<{ watch: void; provokesCspViolation: boolean }>({
  provokesCspViolation: [false, { option: true }],
  watch: [
    async ({ page, provokesCspViolation }, use) => {
      const problems: string[] = [];
      await page.addInitScript(() => {
        document.addEventListener('securitypolicyviolation', (event) => {
          console.error(`CSP violation: ${event.violatedDirective} blocked ${event.blockedURI || 'inline'} (${event.sourceFile ?? ''}:${event.lineNumber})`);
        });
      });
      page.on('pageerror', (error) => problems.push(`uncaught: ${error.message}`));
      page.on('console', (message) => {
        const text = message.text();
        if (provokesCspViolation) return;
        // Browser errors about the policy, and warnings about a header it could not parse (an unknown Permissions-Policy feature).
        if (message.type() === 'error' && /Content Security Policy|CSP violation|Refused to|violates the following/i.test(text)) problems.push(text);
        if (/Error with Permissions-Policy header/i.test(text)) problems.push(text);
      });
      page.on('requestfailed', (request) => {
        if (['script', 'stylesheet', 'font', 'image'].includes(request.resourceType()) && request.url().startsWith('http://localhost')) {
          problems.push(`failed ${request.resourceType()}: ${request.url()} ${request.failure()?.errorText ?? ''}`);
        }
      });
      await use();
      expect(problems, 'CSP violations, uncaught errors, failed assets').toEqual([]);
    },
    { auto: true },
  ],
});

export { expect };
