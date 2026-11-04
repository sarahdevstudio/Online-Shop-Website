// Staff and roles in the browser (docs/16): create a role and an account through the forms, the new person sees only what the role
// allows, deactivating ends their session on the next request, a password reset replaces the password, and the lock-out rule shows
// its message. axe runs on every staff screen.
import { expect, test } from './fixtures';
import { ADMIN_PASS, ADMIN_USER, BASE, RUN, axeCheck, check, login, shot } from './helpers';

const STAFF_PASSWORD = `Staff-${RUN}-Pw-2026!`;
const NEW_PASSWORD = `Staff-${RUN}-New-2027!`;
const username = `blogger-${RUN}`.slice(0, 40);
const roleName = `Blogger${RUN}`.slice(0, 40);

async function stepUp(page: import('@playwright/test').Page, scope = page.locator('body')) {
  await scope.locator('input[name="currentPassword"]').fill(ADMIN_PASS);
}

test.describe.configure({ mode: 'serial' });

test('create a role through the form', async ({ page }) => {
  await login(page);
  await page.goto('/admin/staff');
  await axeCheck(page, '/admin/staff');
  await shot(page, 'staff-list');
  await page.goto('/admin/staff/roles');
  await axeCheck(page, '/admin/staff/roles');
  await page.goto('/admin/staff/roles/new');
  await axeCheck(page, '/admin/staff/roles/new');
  await page.fill('input[name="name"]', roleName);
  await page.fill('input[name="title"]', `نویسنده ${RUN}`);
  await page.check('input[name="permissions"][value="Blog.Manage"]');
  // Without the caller's password the form is refused next to that box.
  await page.locator('form button[type="submit"]').last().click();
  await expect(page.locator('.ui-error').first()).toBeVisible();
  await stepUp(page);
  await Promise.all([page.waitForURL(/\/admin\/staff\/roles\/[0-9a-f-]{36}$/), page.locator('form button[type="submit"]').last().click()]);
  await expect(page.locator('input[name="name"]')).toHaveValue(roleName);
  await expect(page.locator('input[name="permissions"][value="Blog.Manage"]')).toBeChecked();
  await axeCheck(page, '/admin/staff/roles/[id]');
});

test('create an account with that role; the person sees only the blog', async ({ page, browser }) => {
  await login(page);
  await page.goto('/admin/staff/new');
  await axeCheck(page, '/admin/staff/new');
  await page.fill('input[name="username"]', username);
  await page.fill('input[name="displayName"]', `نویسندهٔ آزمایشی ${RUN}`);
  await page.fill('input[name="password"]', 'short');
  await page.check(`input[name="roles"][value="${roleName}"]`);
  await stepUp(page);
  await page.locator('form button[type="submit"]').last().click();
  await expect(page.locator('.ui-error').first()).toBeVisible(); // the admin password rules apply
  await page.fill('input[name="password"]', STAFF_PASSWORD);
  await stepUp(page);
  await Promise.all([page.waitForURL(/\/admin\/staff\/[0-9a-f-]{36}$/), page.locator('form button[type="submit"]').last().click()]);
  await expect(page.locator('h1')).toContainText(username);
  await shot(page, 'staff-user');
  await axeCheck(page, '/admin/staff/[id]');

  // The new person signs in (their own browser) and sees only what the role allows.
  const context = await browser.newContext({ baseURL: BASE, locale: 'fa-IR' });
  const blogger = await context.newPage();
  await blogger.goto('/admin/login');
  await blogger.fill('input[name="username"]', username);
  await blogger.fill('input[name="password"]', STAFF_PASSWORD);
  await Promise.all([blogger.waitForURL(/\/admin(\/)?$/), blogger.click('form button[type="submit"]')]);
  const links = await blogger.locator('nav a').evaluateAll((all) => all.map((a) => a.getAttribute('href')));
  check('the menu shows the blog', links.includes('/admin/blog'), links.join(' '));
  check('the menu hides the staff, settings and orders sections', !links.some((href) => ['/admin/staff', '/admin/settings', '/admin/orders'].includes(href ?? '')), links.join(' '));
  await expect(blogger.locator('main')).toContainText('خوش آمدید'); // a welcome text, not a dashboard error
  await axeCheck(blogger, 'dashboard of a limited account');
  await blogger.goto('/admin/blog');
  await expect(blogger.locator('h1').first()).toBeVisible();
  await axeCheck(blogger, '/admin/blog as the limited account');
  const forbidden = await context.request.get(`${BASE}/admin/staff`);
  check('the staff page refuses the limited account', forbidden.status() !== 200 || !(await forbidden.text()).includes('currentPassword'), String(forbidden.status()));

  // Deactivate from the admin: the open session ends at its next request.
  await page.goto('/admin/staff');
  await page.getByRole('link', { name: username }).click();
  await page.waitForURL(/\/admin\/staff\/[0-9a-f-]{36}$/);
  await Promise.all([page.waitForLoadState('networkidle'), page.getByRole('button', { name: 'غیرفعال کردن حساب' }).click()]);
  await page.waitForTimeout(600);
  await expect(page.getByRole('button', { name: 'فعال کردن حساب' })).toBeVisible();
  await blogger.goto('/admin/blog');
  expect(new URL(blogger.url()).pathname, 'the deactivated person is sent to the login page').toBe('/admin/login');
  await expect(blogger.locator('main')).toContainText('نشست شما پایان یافته');
  await context.close();
});

test('reset the password and reactivate; the new password works, the old one does not', async ({ page, browser }) => {
  await login(page);
  await page.goto('/admin/staff');
  await page.getByRole('link', { name: username }).click();
  await page.waitForURL(/\/admin\/staff\/[0-9a-f-]{36}$/);
  await Promise.all([page.waitForLoadState('networkidle'), page.getByRole('button', { name: 'فعال کردن حساب' }).click()]);
  await page.waitForTimeout(600);

  const reset = page.locator('form:has(input[name="newPassword"])');
  await reset.locator('input[name="newPassword"]').fill(NEW_PASSWORD);
  await stepUp(page, reset);
  await Promise.all([page.waitForLoadState('networkidle'), reset.locator('button[type="submit"]').click()]);
  await page.waitForTimeout(600);
  await expect(page.locator('main')).toContainText('رمز عبور تغییر کرد');

  for (const [password, ok] of [[STAFF_PASSWORD, false], [NEW_PASSWORD, true]] as const) {
    const context = await browser.newContext({ baseURL: BASE, locale: 'fa-IR' });
    const p = await context.newPage();
    await p.goto('/admin/login');
    await p.fill('input[name="username"]', username);
    await p.fill('input[name="password"]', password);
    await p.click('form button[type="submit"]');
    await p.waitForLoadState('networkidle');
    await p.waitForTimeout(600);
    expect(new URL(p.url()).pathname === '/admin' || new URL(p.url()).pathname === '/admin/', `sign-in with ${ok ? 'the new' : 'the old'} password`).toBe(ok);
    await context.close();
  }
});

test('the no-lock-out rule explains itself, and a custom role in use cannot be deleted', async ({ page }) => {
  await login(page);
  // The signed-in admin cannot deactivate their own account.
  await page.goto('/admin/staff');
  await page.getByRole('link', { name: ADMIN_USER, exact: true }).click();
  await page.waitForURL(/\/admin\/staff\/[0-9a-f-]{36}$/);
  await expect(page.locator('main')).toContainText('این حساب خود شماست');

  // The role that still has a member cannot be removed.
  await page.goto('/admin/staff/roles');
  await page.getByRole('link', { name: `نویسنده ${RUN}` }).click();
  await page.waitForURL(/\/admin\/staff\/roles\/[0-9a-f-]{36}$/);
  await page.locator('details summary').click();
  const del = page.locator('details form');
  await stepUp(page, del);
  await Promise.all([page.waitForLoadState('networkidle'), del.locator('button[type="submit"]').click()]);
  await page.waitForTimeout(600);
  await expect(page.locator('main')).toContainText('هنوز به کاربری داده شده');
});
