// Automated UI/UX review of the style guide in a real (headless) Chrome:
//  - accessibility audit (axe-core, WCAG 2.2 A/AA + best practices) on every page x viewport x theme x direction
//  - horizontal-overflow check (a classic RTL/mobile bug)
//  - interaction checks: drawer/modal geometry, Escape + backdrop close, focus return, button states
//  - full-page screenshots into review-output/ for visual inspection
//
//  Requires the style guide to be running:  npx ng serve styleguide --port 4300
//  Usage: node tools/review.mjs [--base http://localhost:4300] [--no-shots]
import puppeteer from 'puppeteer-core';
import { mkdirSync, readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));
const args = process.argv.slice(2);
const base = args.includes('--base') ? args[args.indexOf('--base') + 1] : 'http://localhost:4300';
const shots = !args.includes('--no-shots');
const outDir = join(here, '..', 'review-output');
mkdirSync(outDir, { recursive: true });

const CHROME = process.env.CHROME_PATH ?? 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
const axeSource = readFileSync(join(here, '..', 'node_modules', 'axe-core', 'axe.min.js'), 'utf8');

const viewports = [
  { name: 'mobile', width: 375, height: 812 },
  { name: 'tablet', width: 768, height: 1024 },
  { name: 'desktop', width: 1280, height: 900 },
];
const pages = ['tokens', 'components', 'layout'];
const variants = [
  { theme: 'light', dir: 'rtl' },
  { theme: 'dark', dir: 'rtl' },
  { theme: 'light', dir: 'ltr' },
];

const problems = [];
const note = (kind, where, detail) => {
  problems.push({ kind, where, detail });
  console.log(`  ✗ [${kind}] ${where}: ${detail}`);
};
const ok = (msg) => console.log(`  ✓ ${msg}`);

const browser = await puppeteer.launch({ executablePath: CHROME, headless: true, args: ['--no-sandbox'] });

async function open(viewport, pageName, variant) {
  const page = await browser.newPage();
  await page.setViewport({ width: viewport.width, height: viewport.height, deviceScaleFactor: 1, isMobile: viewport.name === 'mobile', hasTouch: viewport.name === 'mobile' });
  const errors = [];
  page.on('pageerror', (e) => errors.push(String(e)));
  page.on('console', (m) => m.type() === 'error' && errors.push(m.text()));
  await page.goto(`${base}/${pageName}`, { waitUntil: 'networkidle0' });
  await page.evaluate(({ theme, dir }) => {
    document.documentElement.setAttribute('data-theme', theme);
    document.documentElement.setAttribute('dir', dir);
    document.documentElement.setAttribute('lang', dir === 'rtl' ? 'fa' : 'en');
  }, variant);
  await page.evaluate(() => document.fonts.ready);
  return { page, errors };
}

console.log(`Review against ${base}\n`);

// ---------- 1. Audit matrix ----------
for (const viewport of viewports) {
  for (const pageName of pages) {
    for (const variant of variants) {
      const label = `${pageName} · ${viewport.name} · ${variant.theme}/${variant.dir}`;
      const { page, errors } = await open(viewport, pageName, variant);

      await page.evaluate(axeSource);
      const result = await page.evaluate(() =>
        axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa', 'best-practice'] } }),
      );
      const violations = result.violations.map((v) => `${v.id} (${v.impact}) x${v.nodes.length}: ${v.nodes[0].target.join(' ')}`);

      const overflow = await page.evaluate(() => {
        const doc = document.documentElement;
        const offenders = [...document.querySelectorAll('body *')]
          .filter((el) => {
            const r = el.getBoundingClientRect();
            const style = getComputedStyle(el);
            return r.width > 0 && style.position !== 'fixed' && (r.right > doc.clientWidth + 1 || r.left < -1);
          })
          .slice(0, 3)
          .map((el) => el.tagName.toLowerCase() + (el.className && typeof el.className === 'string' ? '.' + el.className.split(' ')[0] : ''));
        return { scroll: doc.scrollWidth > doc.clientWidth + 1, offenders };
      });

      if (errors.length) note('console', label, errors[0]);
      if (violations.length) violations.forEach((v) => note('a11y', label, v));
      if (overflow.scroll) note('overflow', label, `horizontal scroll; offenders: ${overflow.offenders.join(', ')}`);
      if (!errors.length && !violations.length && !overflow.scroll) ok(label);

      if (shots && variant.theme === 'light' && variant.dir === 'rtl') {
        await page.screenshot({ path: join(outDir, `${pageName}-${viewport.name}-${variant.theme}-${variant.dir}.png`), fullPage: true });
      }
      if (shots && variant.theme === 'dark' && viewport.name !== 'tablet') {
        await page.screenshot({ path: join(outDir, `${pageName}-${viewport.name}-dark-rtl.png`), fullPage: true });
      }
      await page.close();
    }
  }
}

// ---------- 2. Interaction checks (components page) ----------
console.log('\nInteractions');
for (const viewport of [viewports[0], viewports[2]]) {
  for (const dir of ['rtl', 'ltr']) {
    const label = `${viewport.name}/${dir}`;
    const { page } = await open(viewport, 'components', { theme: 'light', dir });
    const wait = (ms) => new Promise((r) => setTimeout(r, ms));
    const openDrawerButtons = await page.$$('#overlays button');

    // Drawer (end side): hugs the inline-end edge, fully inside the viewport after the slide.
    await openDrawerButtons[1].click();
    await wait(600);
    const end = await page.evaluate(() => {
      const d = [...document.querySelectorAll('ui-drawer dialog')].find((x) => x.open);
      if (!d) return null;
      const r = d.querySelector('.panel').getBoundingClientRect();
      return { left: r.left, right: r.right, vw: innerWidth, focus: document.activeElement?.getAttribute('aria-label') ?? document.activeElement?.tagName, scrollLocked: getComputedStyle(document.body).overflow };
    });
    if (!end) note('drawer', label, 'did not open');
    else {
      const hugsEnd = dir === 'rtl' ? end.left < 1 : Math.abs(end.right - end.vw) < 1;
      if (!hugsEnd) note('drawer', label, `end drawer not on the inline-end edge (left ${end.left}, right ${end.right}, vw ${end.vw})`);
      else ok(`${label}: end drawer sits on the ${dir === 'rtl' ? 'left' : 'right'} edge`);
      if (end.scrollLocked !== 'hidden') note('drawer', label, 'page scroll not locked while open');
      if (end.focus !== 'بستن' && end.focus !== 'Close') note('drawer', label, `focus not moved into the dialog (on ${end.focus})`);
    }
    await page.keyboard.press('Escape');
    await wait(300);
    const closedByEsc = await page.evaluate(() => ![...document.querySelectorAll('dialog')].some((d) => d.open));
    closedByEsc ? ok(`${label}: Escape closes the drawer`) : note('drawer', label, 'Escape did not close');
    const focusBack = await page.evaluate(() => document.activeElement?.textContent?.trim());
    focusBack === 'دراور (سمت انتها)' ? ok(`${label}: focus returned to the trigger`) : note('drawer', label, `focus not returned (on "${focusBack}")`);

    // Drawer (start side)
    await openDrawerButtons[2].click();
    await wait(600);
    const start = await page.evaluate(() => {
      const d = [...document.querySelectorAll('ui-drawer dialog')].find((x) => x.open);
      const r = d?.querySelector('.panel').getBoundingClientRect();
      return r ? { left: r.left, right: r.right, vw: innerWidth } : null;
    });
    const hugsStart = start && (dir === 'rtl' ? Math.abs(start.right - start.vw) < 1 : start.left < 1);
    hugsStart ? ok(`${label}: start drawer sits on the ${dir === 'rtl' ? 'right' : 'left'} edge`) : note('drawer', label, `start drawer misplaced ${JSON.stringify(start)}`);
    await page.keyboard.press('Escape');
    await wait(300);

    // Modal: centered; backdrop click closes.
    await openDrawerButtons[0].click();
    await wait(600);
    const modal = await page.evaluate(() => {
      const d = [...document.querySelectorAll('ui-modal dialog')].find((x) => x.open);
      const r = d?.querySelector('.panel').getBoundingClientRect();
      return r ? { cx: r.left + r.width / 2, vw: innerWidth, fits: r.left >= 0 && r.right <= innerWidth } : null;
    });
    modal && Math.abs(modal.cx - modal.vw / 2) < 2 && modal.fits ? ok(`${label}: modal centered and inside the viewport`) : note('modal', label, `bad geometry ${JSON.stringify(modal)}`);
    await page.mouse.click(2, 2); // backdrop
    await wait(300);
    const closedByBackdrop = await page.evaluate(() => ![...document.querySelectorAll('dialog')].some((d) => d.open));
    closedByBackdrop ? ok(`${label}: backdrop click closes the modal`) : note('modal', label, 'backdrop click did not close');

    // Buttons: disabled stays disabled; loading swallows clicks.
    const buttons = await page.evaluate(async () => {
      const disabled = [...document.querySelectorAll('button[uiButton]')].find((b) => b.textContent.trim() === 'غیرفعال');
      const loading = [...document.querySelectorAll('button[uiButton]')].find((b) => b.textContent.includes('ثبت سفارش') || b.textContent.includes('در حال ثبت'));
      let clicks = 0;
      loading.addEventListener('click', () => clicks++);
      loading.click(); // starts loading (handler in the page)
      await new Promise((r) => setTimeout(r, 200));
      const busy = loading.getAttribute('aria-busy');
      loading.click();
      loading.click();
      return { disabledAttr: disabled?.hasAttribute('disabled'), opacity: getComputedStyle(disabled).opacity, busy, clicksWhileLoading: clicks };
    });
    buttons.disabledAttr && Number(buttons.opacity) < 1 ? ok(`${label}: disabled button keeps its attribute and looks disabled`) : note('button', label, `disabled button broken ${JSON.stringify(buttons)}`);
    buttons.busy === 'true' && buttons.clicksWhileLoading === 1 ? ok(`${label}: loading button sets aria-busy and ignores repeat clicks`) : note('button', label, `loading button ${JSON.stringify(buttons)}`);

    await page.close();
  }
}

// ---------- 3. Layout page: header behaviour ----------
console.log('\nHeader');
for (const viewport of viewports) {
  const { page } = await open(viewport, 'layout', { theme: 'light', dir: 'rtl' });
  const header = await page.evaluate(() => {
    const menuBtn = document.querySelector('.menu-btn');
    const nav = document.querySelector('.primary');
    const search = document.querySelector('.search input');
    const sr = search.getBoundingClientRect();
    return {
      menuVisible: getComputedStyle(menuBtn).display !== 'none',
      navVisible: getComputedStyle(nav).display !== 'none',
      searchWidth: Math.round(sr.width),
      headerHeight: Math.round(document.querySelector('.header').getBoundingClientRect().height),
      stickyTop: getComputedStyle(document.querySelector('.header')).position,
    };
  });
  const wantsDesktop = viewport.width >= 1024;
  header.menuVisible === !wantsDesktop && header.navVisible === wantsDesktop
    ? ok(`${viewport.name}: ${wantsDesktop ? 'inline nav, no hamburger' : 'hamburger, nav in drawer'}; header ${header.headerHeight}px, search ${header.searchWidth}px, ${header.stickyTop}`)
    : note('header', viewport.name, JSON.stringify(header));
  await page.close();
}

await browser.close();

console.log(`\n${problems.length === 0 ? 'REVIEW PASSED' : `REVIEW FOUND ${problems.length} PROBLEM(S)`}`);
if (shots) console.log(`Screenshots: ${outDir}`);
process.exit(problems.length === 0 ? 0 : 1);
