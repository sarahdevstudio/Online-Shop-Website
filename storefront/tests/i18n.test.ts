import fs from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';
import adminDefaults from '../src/lib/i18n/defaults.admin.json';
import shopDefaults from '../src/lib/i18n/defaults.shop.json';
import { currentTexts, setTexts, t, tf } from '../src/lib/i18n/texts';
import '../src/lib/messages';
import '../src/lib/admin/messages';

const root = path.resolve(__dirname, '..', '..');
const manifest = JSON.parse(fs.readFileSync(path.join(root, 'translations', 'fa-IR.json'), 'utf8')) as Record<string, string>;
const pick = (prefixes: string[]) => Object.fromEntries(Object.entries(manifest).filter(([k]) => prefixes.some((p) => k.startsWith(p))));

describe('shipped texts', () => {
  it('the storefront bundles are the manifest, split by area (run `npm run i18n:sync` after editing translations/fa-IR.json)', () => {
    expect(shopDefaults).toEqual(pick(['common.', 'shop.', 'seo.']));
    expect(adminDefaults).toEqual(pick(['admin.', 'common.']));
  });

  it('never ships api.* texts to the browser bundles', () => {
    expect(Object.keys(shopDefaults).concat(Object.keys(adminDefaults)).filter((k) => k.startsWith('api.'))).toEqual([]);
  });
});

describe('t() and tf()', () => {
  it('returns the shipped text, an edited text when one is loaded, and never the key', () => {
    setTexts({});
    expect(t('shop.not-found-title')).toBe(manifest['shop.not-found-title']);
    setTexts({ 'shop.not-found-title': 'اینجا چیزی نیست' });
    try {
      expect(t('shop.not-found-title')).toBe('اینجا چیزی نیست');
      expect(currentTexts()['shop.not-found-title']).toBe('اینجا چیزی نیست');
    } finally {
      setTexts({});
    }
  });

  it('fills named placeholders and leaves unknown ones as written', () => {
    setTexts({ 'shop.listing-page-title': '{name} / {page} / {other}' });
    try {
      expect(tf('shop.listing-page-title', { name: 'لامپ', page: '۲' })).toBe('لامپ / ۲ / {other}');
    } finally {
      setTexts({});
    }
  });
});

describe('no hard-coded Persian text', () => {
  /** Files that legitimately hold Persian-script characters that are not UI text: digit tables, character classes, a Persian-only filter. */
  const ALLOWED: Record<string, string> = {
    'lib/format.ts': 'Persian/Arabic digit tables and separators used for number formatting',
    'lib/contrast.ts': 'Persian digit table for ratios',
    'lib/persian-only.ts': 'the Persian-script character class that guards against English text reaching visitors',
    'app/admin/(panel)/site/home/HomeEditor.tsx': 'a character class that splits product ids on Persian and Latin commas',
  };

  function walk(dir: string): string[] {
    return fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) return walk(full);
      return /\.(ts|tsx)$/.test(entry.name) ? [full] : [];
    });
  }

  it('has no Persian/Arabic-script text in storefront or admin source outside the allowed files', () => {
    const src = path.resolve(__dirname, '..', 'src');
    const found: string[] = [];
    for (const file of walk(src)) {
      const rel = path.relative(src, file).split(path.sep).join('/');
      if (rel in ALLOWED) continue;
      fs.readFileSync(file, 'utf8')
        .split('\n')
        .forEach((line, index) => {
          const trimmed = line.trim();
          if (trimmed.startsWith('//') || trimmed.startsWith('*') || trimmed.startsWith('/*')) return;
          if (/[؀-ۿ]/.test(line)) found.push(`${rel}:${index + 1}: ${trimmed.slice(0, 80)}`);
        });
    }
    expect(found, 'Move the text to translations/fa-IR.json and read it through messages / t():\n' + found.join('\n')).toEqual([]);
  });
});
