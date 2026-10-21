// Verifies WCAG 2.2 contrast for every text/background and UI-component pair in the design tokens,
// in both themes. Fails (exit 1) on any pair below its minimum, so a bad color change cannot ship.
//
//   node tools/check-contrast.mjs
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));
const css = readFileSync(join(here, '../projects/shared-ui/src/styles/tokens.css'), 'utf8');

function block(selectorPattern) {
  const match = css.match(new RegExp(`${selectorPattern}\\s*\\{([\\s\\S]*?)\\n\\}`));
  if (!match) throw new Error(`Block not found: ${selectorPattern}`);
  const vars = {};
  for (const [, name, value] of match[1].matchAll(/(--color-[\w-]+)\s*:\s*(#[0-9a-fA-F]{6})\s*;/g)) {
    vars[name] = value;
  }
  return vars;
}

const light = block(':root');
const dark = { ...light, ...block("\\[data-theme='dark'\\]") };

function luminance(hex) {
  const channels = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255);
  const [r, g, b] = channels.map((c) => (c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4));
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

function ratio(a, b) {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (hi + 0.05) / (lo + 0.05);
}

// [foreground, background, minimum ratio, what it is]
// 4.5 = normal text (AA), 3 = large text / UI components / meaningful graphics (AA).
const TEXT = 4.5;
const UI = 3;
const pairs = [
  ['--color-text', '--color-bg', 7, 'body text'],
  ['--color-text', '--color-surface-muted', TEXT, 'text on muted surface'],
  ['--color-text', '--color-surface-sunken', TEXT, 'text on sunken surface'],
  ['--color-text-muted', '--color-bg', TEXT, 'muted text'],
  ['--color-text-muted', '--color-surface-muted', TEXT, 'muted text on muted surface'],
  ['--color-text-subtle', '--color-bg', TEXT, 'subtle text'],
  ['--color-text-subtle', '--color-surface-muted', TEXT, 'subtle text on muted surface'],
  ['--color-text-subtle', '--color-surface-sunken', TEXT, 'subtle text / placeholder on sunken'],
  ['--color-primary', '--color-bg', TEXT, 'link / primary text'],
  ['--color-primary', '--color-surface-muted', TEXT, 'link on muted surface'],
  ['--color-primary-hover', '--color-bg', TEXT, 'link hover'],
  ['--color-primary-contrast', '--color-primary', TEXT, 'button label'],
  ['--color-primary-contrast', '--color-primary-hover', TEXT, 'button label (hover)'],
  ['--color-primary-contrast', '--color-primary-active', TEXT, 'button label (active)'],
  ['--color-primary', '--color-primary-soft', TEXT, 'text on primary-soft'],
  ['--color-danger', '--color-bg', TEXT, 'error text'],
  ['--color-danger', '--color-danger-soft', TEXT, 'error on soft'],
  ['--color-danger-contrast', '--color-danger', TEXT, 'danger button label'],
  ['--color-danger-contrast', '--color-danger-hover', TEXT, 'danger button label (hover)'],
  ['--color-success', '--color-success-soft', TEXT, 'success on soft'],
  ['--color-success', '--color-bg', TEXT, 'success text'],
  ['--color-warning', '--color-warning-soft', TEXT, 'warning on soft'],
  ['--color-warning', '--color-bg', TEXT, 'warning text'],
  ['--color-info', '--color-info-soft', TEXT, 'info on soft'],
  ['--color-sale-contrast', '--color-sale', TEXT, 'sale badge label'],
  ['--color-sale', '--color-bg', UI, 'sale accent as graphic'],
  ['--color-border-control', '--color-bg', UI, 'input border'],
  ['--color-border-control', '--color-surface', UI, 'input border on surface'],
  ['--color-focus', '--color-bg', UI, 'focus ring on page'],
  ['--color-focus', '--color-surface-muted', UI, 'focus ring on muted surface'],
  ['--color-star', '--color-bg', UI, 'rating star'],
  ['--color-star-empty', '--color-bg', 2.4, 'empty rating star (decorative, value also in text)'],
];

let failures = 0;
for (const [themeName, vars] of [['light', light], ['dark', dark]]) {
  for (const [fg, bg, min, label] of pairs) {
    if (!vars[fg] || !vars[bg]) {
      console.error(`MISSING token for pair ${fg} / ${bg} (${themeName})`);
      failures++;
      continue;
    }
    const value = ratio(vars[fg], vars[bg]);
    const ok = value >= min;
    if (!ok) failures++;
    console.log(`${ok ? 'ok  ' : 'FAIL'} ${themeName.padEnd(5)} ${value.toFixed(2).padStart(5)} (min ${min})  ${label}: ${fg} on ${bg}`);
  }
}

if (failures > 0) {
  console.error(`\n${failures} contrast check(s) failed.`);
  process.exit(1);
}
console.log(`\nAll ${pairs.length * 2} contrast checks passed.`);
