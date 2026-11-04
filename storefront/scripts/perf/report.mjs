// Median of the Lighthouse runs per page, as a Markdown table.
//   node scripts/perf/report.mjs <dir with lhr-*.json> [label]
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

const dir = process.argv[2];
if (!dir) throw new Error('usage: report.mjs <dir with lhr-*.json>');

const median = (values) => {
  const sorted = [...values].sort((a, b) => a - b);
  return sorted[Math.floor(sorted.length / 2)];
};
const kb = (bytes) => `${(bytes / 1024).toFixed(0)} KB`;

const runs = new Map();
for (const file of readdirSync(dir).filter((f) => /^lhr-.*\.json$/.test(f))) {
  const lhr = JSON.parse(readFileSync(join(dir, file), 'utf8'));
  const page = new URL(lhr.finalDisplayedUrl ?? lhr.finalUrl).pathname;
  const summary = Object.fromEntries(lhr.audits['resource-summary'].details.items.map((i) => [i.resourceType, i]));
  const row = {
    score: (lhr.categories.performance.score ?? 0) * 100,
    lcp: lhr.audits['largest-contentful-paint'].numericValue,
    cls: lhr.audits['cumulative-layout-shift'].numericValue,
    tbt: lhr.audits['total-blocking-time'].numericValue,
    ttfb: lhr.audits['server-response-time'].numericValue,
    js: summary.script?.transferSize ?? 0,
    image: summary.image?.transferSize ?? 0,
    font: summary.font?.transferSize ?? 0,
    total: summary.total?.transferSize ?? 0,
    lcpElement: lhr.audits['largest-contentful-paint-element']?.details?.items?.[0]?.items?.[0]?.node?.snippet ?? '',
  };
  runs.set(page, [...(runs.get(page) ?? []), row]);
}

console.log('| Page | Score | LCP | CLS | TBT | TTFB | JS | Images | Fonts | Total |');
console.log('|---|---|---|---|---|---|---|---|---|---|');
for (const [page, rows] of runs) {
  const m = (key) => median(rows.map((r) => r[key]));
  console.log(`| ${page} | ${m('score').toFixed(0)} | ${(m('lcp') / 1000).toFixed(2)} s | ${m('cls').toFixed(3)} | ${m('tbt').toFixed(0)} ms | ${m('ttfb').toFixed(0)} ms | ${kb(m('js'))} | ${kb(m('image'))} | ${kb(m('font'))} | ${kb(m('total'))} |`);
}
console.log('');
for (const [page, rows] of runs) console.log(`LCP element ${page}: ${rows[0].lcpElement.slice(0, 140)}`);
