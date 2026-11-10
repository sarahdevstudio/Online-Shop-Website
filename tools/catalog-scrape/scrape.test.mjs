// node --test tools/catalog-scrape/scrape.test.mjs
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { rialToToman, decodeEntities } from './scrape.mjs';

test('Rial to Toman: divides by 10', () => {
  assert.equal(rialToToman(28_000_000, 'test'), 2_800_000);
});

test('Rial to Toman: rejects a price that is not a whole Toman amount', () => {
  assert.throws(() => rialToToman(28_000_005, 'test'));
});

test('decodeEntities: decodes named and numeric entities', () => {
  assert.equal(decodeEntities('قابل تغییر &#8230;'), 'قابل تغییر …');
  assert.equal(decodeEntities('a &amp; b &nbsp; &quot;x&quot; &times; &#x2026;'), 'a & b   "x" × …');
});
