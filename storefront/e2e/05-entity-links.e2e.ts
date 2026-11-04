// Entity links in the site configuration (docs/13-dynamic-routing.md): picked in the editor, rendered with the CURRENT path, hidden
// while the target is a draft or deleted (the editor names it and its state), back when published again.
// Ported from the former scripts/smoke-entity-links.cjs. It publishes a header/footer version and changes a category path.
import type { Page } from '@playwright/test';
import { test } from './fixtures';
import { BASE, RUN, api, axeCheck, check, eq, hrefs, login, raw, shot } from './helpers';

/** Picks an entity in the searchable select named `name`: choose the kind, type, wait for the list, click the first option. */
async function pickEntity(page: Page, name: string, kind: string, text: string) {
  await page.selectOption(`select[name="${name}.kind"]`, kind);
  const box = page.locator(`input[role="combobox"][name="${name}"]`);
  await box.click();
  await box.fill(text);
  const option = page.locator('[role="listbox"] [role="option"]', { hasText: text }).first(); // the list still shows the previous search until the new one answers
  await option.waitFor({ timeout: 8000 });
  await option.click();
  await page.waitForTimeout(150);
}

async function intent(page: Page, value: string) {
  await Promise.all([page.waitForLoadState('networkidle'), page.click(`button[name="intent"][value="${value}"]`)]);
  await page.waitForTimeout(700);
}

test('entity links: picked, current path, hidden until published, deleted warning', async ({ page, context }) => {
  await login(page);

  const tree = await api('/catalog/categories');
  const category = tree.flatMap((c: any) => [c, ...c.children]).find((c: any) => c.slug === 'lamps') ?? tree[0];
  const oldPath = category.path;

  // a post to link to (and delete later)
  const slug = `smoke-link-post-${RUN}`;
  await page.goto(`${BASE}/admin/blog/new`);
  await page.fill('input[name="title"]', `نوشتهٔ پیوند ${RUN}`);
  await page.fill('input[name="slug"]', slug);
  await page.fill('textarea[name="excerpt"]', 'خلاصه');
  await page.fill('textarea[name="body"]', 'متن');
  await page.check('input[name="publish"]');
  await Promise.all([page.waitForURL(/\/admin\/blog\/\d+$/), page.click('form:has(input[name="path"]) button[type="submit"]')]);
  const postId = page.url().split('/').pop();

  // ---- 1. build a menu and a footer with entity links in the editor ----------------------------------------------------
  await page.goto(`${BASE}/admin/site/header-footer`);
  const menuCount = await page.locator('input[name^="headerMenu["][name$=".label"]').count();
  const n = menuCount;
  await page.getByRole('button', { name: 'افزودن آیتم' }).click();
  await page.fill(`input[name="headerMenu[${n}].label"]`, 'منوی دسته');
  await pickEntity(page, `headerMenu[${n}].link`, 'category', category.name);
  eq('the picked category shows its name in the box', await page.locator(`input[role="combobox"][name="headerMenu[${n}].link"]`).inputValue(), category.name);
  check('the hint shows the current path', (await page.locator('main').innerText()).includes(oldPath));
  await page.getByRole('button', { name: 'افزودن آیتم' }).click();
  await page.fill(`input[name="headerMenu[${n + 1}].label"]`, 'منوی دلخواه');
  await page.fill(`input[name="headerMenu[${n + 1}].link"]`, oldPath);

  await page.getByRole('button', { name: 'افزودن ستون' }).click();
  const column = await page.locator('input[name^="footer.columns["][name$=".title"]').count() - 1;
  await page.fill(`input[name="footer.columns[${column}].title"]`, 'پیوندها');
  await page.fill(`input[name="footer.columns[${column}].links[0].label"]`, 'دسته');
  await pickEntity(page, `footer.columns[${column}].links[0].link`, 'category', category.name);
  await page.getByRole('button', { name: 'افزودن پیوند' }).last().click();
  await page.fill(`input[name="footer.columns[${column}].links[1].label"]`, 'نوشته');
  await pickEntity(page, `footer.columns[${column}].links[1].link`, 'blogPost', `نوشتهٔ پیوند ${RUN}`);
  await shot(page, '10-editor-entity-links');
  await axeCheck(page, '/admin/site/header-footer (entity links)');

  // the open list is accessible too
  await page.selectOption(`select[name="footer.columns[${column}].links[1].link.kind"]`, 'product');
  await page.locator(`input[role="combobox"][name="footer.columns[${column}].links[1].link"]`).click();
  await page.locator('[role="listbox"] [role="option"]').first().waitFor({ timeout: 8000 });
  await shot(page, '11-editor-combobox-open');
  await axeCheck(page, '/admin/site/header-footer (combobox open)');
  await page.keyboard.press('Escape');
  await pickEntity(page, `footer.columns[${column}].links[1].link`, 'blogPost', `نوشتهٔ پیوند ${RUN}`);

  await intent(page, 'publish');
  const saved = await page.locator('main').innerText();
  check('publishing succeeds', !saved.includes('اصلاح'), saved.slice(0, 120).replace(/\s+/g, ' '));

  // ---- 2. the storefront shows the entity's path ------------------------------------------------------------------------
  await page.goto(`${BASE}/`);
  let header = await hrefs(page, 'header a');
  let footer = await hrefs(page, 'footer a');
  check('menu: the category entity link renders the current path', header.includes(oldPath), header.join(' '));
  check('footer: the category and the post entity links render', footer.includes(oldPath) && footer.includes(`/blog/${slug}`), footer.join(' '));

  // ---- 3. change the category path: menu and footer show the new path DIRECTLY ---------------------------------------
  await page.goto(`${BASE}/admin/categories`);
  const detail = page.locator('details', { hasText: category.slug }).first();
  await detail.locator('summary').click();
  const newPath = `/shop/links-${RUN}`;
  await detail.locator('input[name="path"]').fill(newPath);
  await Promise.all([page.waitForLoadState('networkidle'), detail.locator('form:has(input[name="path"]) button[type="submit"]').first().click()]);
  await page.waitForTimeout(800);
  await page.goto(`${BASE}/`);
  header = await hrefs(page, 'header a');
  footer = await hrefs(page, 'footer a');
  check('menu shows the NEW path', header.includes(newPath), header.join(' '));
  check('footer shows the NEW path', footer.includes(newPath), footer.join(' '));
  const entityHrefs = [header.filter((h) => h === newPath).length, footer.filter((h) => h === newPath).length];
  check('the entity links answer 200 at once (no 308 hop)', (await raw(context, newPath)).status === 200, `links to ${newPath}: header ${entityHrefs[0]}, footer ${entityHrefs[1]}`);
  const typedHop = await raw(context, oldPath);
  check('the typed link still points at the old path and hops through a 308', header.includes(oldPath) && typedHop.status === 308 && typedHop.location === newPath, `${typedHop.status} ${typedHop.location}`);
  await shot(page, '12-storefront-after-path-change');

  // ---- 3b. unpublish the post: the link is hidden and the editor names it and its state; publish again: it comes back ------
  const setPublished = async (published: boolean) => {
    await page.goto(`${BASE}/admin/blog/${postId}`);
    await (published ? page.check('input[name="publish"]') : page.uncheck('input[name="publish"]'));
    await Promise.all([page.waitForLoadState('networkidle'), page.click('form:has(input[name="path"]) button[type="submit"]')]);
    await page.waitForTimeout(700);
  };
  await setPublished(false);
  await page.goto(`${BASE}/`);
  footer = await hrefs(page, 'footer a');
  check('an unpublished post\'s footer link is hidden', !footer.includes(`/blog/${slug}`) && footer.includes(newPath), footer.join(' '));
  await page.goto(`${BASE}/admin/site/header-footer`);
  let hint = await page.locator('p.ui-error').allInnerTexts();
  check('the editor names the post and says it is a draft', hint.some((w) => w.includes(`نوشتهٔ پیوند ${RUN}`) && w.includes('پیش‌نویس')), hint.join(' | ').slice(0, 200));
  await shot(page, '15-editor-draft-warning');
  await axeCheck(page, '/admin/site/header-footer (draft warning)');
  await setPublished(true);
  await page.goto(`${BASE}/`);
  footer = await hrefs(page, 'footer a');
  check('publishing the post again brings its footer link back', footer.includes(`/blog/${slug}`), footer.join(' '));

  // ---- 4. delete the post: the link is hidden, the editor warns ------------------------------------------------------------
  await page.goto(`${BASE}/admin/blog/${postId}`);
  await page.locator('details summary').last().click();
  await Promise.all([page.waitForURL(/\/admin\/blog$/), page.locator('details button[type="submit"]').click()]);
  await page.goto(`${BASE}/`);
  footer = await hrefs(page, 'footer a');
  check('the deleted post\'s footer link is hidden', !footer.includes(`/blog/${slug}`) && footer.includes(newPath), footer.join(' '));
  await page.goto(`${BASE}/admin/site/header-footer`);
  const warnings = await page.locator('p.ui-error').allInnerTexts();
  check('the editor warns about the deleted target and still names it', warnings.some((w) => w.includes('حذف شده') && w.includes(`نوشتهٔ پیوند ${RUN}`)), warnings.join(' | ').slice(0, 200));
  await shot(page, '13-editor-deleted-warning');
  await axeCheck(page, '/admin/site/header-footer (deleted warning)');

  // ---- 5. the home editor (hero and banner links) uses the same field -----------------------------------------------------
  await page.goto(`${BASE}/admin/site/home`);
  check('the home editor has the link kind select for the hero slide', (await page.locator('select[name$=".link.kind"]').count()) >= 1);
  await shot(page, '14-home-editor-links');
  await axeCheck(page, '/admin/site/home (link fields)');

});
