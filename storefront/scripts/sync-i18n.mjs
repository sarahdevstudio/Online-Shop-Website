// Copies the shipped texts (translations/fa-IR.json, the only place default texts live) into the two bundles the storefront
// imports: the shop bundle (common.*, shop.*, seo.*) and the admin bundle (admin.*, common.*). api.* texts never reach the
// storefront. Run `npm run i18n:sync` after editing translations/fa-IR.json; tests/i18n-sync.test.ts fails when they differ.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const source = JSON.parse(fs.readFileSync(path.join(root, 'translations', 'fa-IR.json'), 'utf8'));
const pick = (prefixes) => Object.fromEntries(Object.entries(source).filter(([k]) => prefixes.some((p) => k.startsWith(p))));
const out = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', 'src', 'lib', 'i18n');
fs.mkdirSync(out, { recursive: true });
for (const [file, prefixes] of [['defaults.shop.json', ['common.', 'shop.', 'seo.']], ['defaults.admin.json', ['admin.', 'common.']]]) {
  fs.writeFileSync(path.join(out, file), JSON.stringify(pick(prefixes), null, 2) + '\n');
}
