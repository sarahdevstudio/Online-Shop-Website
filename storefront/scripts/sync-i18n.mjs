// Copies the shipped texts (translations/fa-IR.json, the only place default texts live) into the two bundles the storefront
// imports: the shop bundle (common.*, shop.*, seo.*) and the admin bundle (admin.*, common.*). api.* texts never reach the
// storefront. Run `npm run i18n:sync` after editing translations/fa-IR.json. `--check` writes nothing: it exits 1 when a bundle
// differs from what the manifest would produce (CI runs it; tests/i18n.test.ts checks the same).
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const source = JSON.parse(fs.readFileSync(path.join(root, 'translations', 'fa-IR.json'), 'utf8'));
const pick = (prefixes) => Object.fromEntries(Object.entries(source).filter(([k]) => prefixes.some((p) => k.startsWith(p))));
const out = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', 'src', 'lib', 'i18n');
const check = process.argv.includes('--check');
const stale = [];
if (!check) fs.mkdirSync(out, { recursive: true });
for (const [file, prefixes] of [['defaults.shop.json', ['common.', 'shop.', 'seo.']], ['defaults.admin.json', ['admin.', 'common.']]]) {
  const target = path.join(out, file);
  const expected = JSON.stringify(pick(prefixes), null, 2) + '\n';
  if (check) {
    const actual = fs.existsSync(target) ? fs.readFileSync(target, 'utf8') : null;
    if (actual !== expected) stale.push(file);
  } else {
    fs.writeFileSync(target, expected);
  }
}
if (stale.length > 0) {
  console.error(`Out of sync with translations/fa-IR.json: ${stale.join(', ')}. Run "npm run i18n:sync" in storefront/ and commit the result.`);
  process.exit(1);
}
if (check) console.log('Bundled texts are in sync with translations/fa-IR.json.');
