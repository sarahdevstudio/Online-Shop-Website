// Fails when CSS uses physical (left/right) properties instead of logical ones, which would break RTL.
// Add `/* rtl-ok */` to a line that is deliberately physical (e.g. a mirrored background-position).
//
//   node tools/check-rtl.mjs
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join, relative } from 'node:path';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', 'projects');

const FORBIDDEN = [
  [/(^|[\s;{])(margin|padding)-(left|right)\s*:/, 'use margin-inline-* / padding-inline-*'],
  [/(^|[\s;{])border-(left|right)(-(width|style|color))?\s*:/, 'use border-inline-start/end'],
  [/(^|[\s;{])border-(top|bottom)-(left|right)-radius\s*:/, 'use border-start-start-radius etc.'],
  [/(^|[\s;{])(left|right)\s*:/, 'use inset-inline-start / inset-inline-end'],
  [/(^|[\s;{])float\s*:\s*(left|right)/, 'avoid float; use flex/grid'],
  [/(^|[\s;{])clear\s*:\s*(left|right)/, 'avoid clear'],
  [/text-align\s*:\s*(left|right)/, 'use text-align: start | end'],
  [/(^|[\s;{])(width|height|min-width|max-width|min-height|max-height)\s*:/, 'use inline-size / block-size (optional but preferred)', 'warn'],
];

function* files(dir) {
  for (const name of readdirSync(dir)) {
    if (name === 'node_modules' || name === 'dist') continue;
    const full = join(dir, name);
    if (statSync(full).isDirectory()) yield* files(full);
    else if (/\.(css|ts|html)$/.test(name) && !name.endsWith('.spec.ts')) yield full;
  }
}

let errors = 0;
for (const file of files(root)) {
  const isTs = file.endsWith('.ts') || file.endsWith('.html');
  const lines = readFileSync(file, 'utf8').split('\n');
  let inStyles = !isTs; // in .ts files only inspect the `styles:` template literal
  lines.forEach((line, index) => {
    if (isTs) {
      if (/styles\s*:\s*`/.test(line)) inStyles = true;
      else if (inStyles && /`,?\s*$/.test(line) && !/styles\s*:/.test(line)) {
        inStyles = false;
        return;
      }
    }
    if (!inStyles || line.includes('rtl-ok')) return;
    for (const [pattern, advice, level] of FORBIDDEN) {
      if (pattern.test(line)) {
        if (level === 'warn') continue; // sizes are tolerated; only direction-sensitive ones fail
        console.error(`${relative(root, file)}:${index + 1}: ${line.trim()}\n   -> ${advice}`);
        errors++;
      }
    }
  });
}

if (errors > 0) {
  console.error(`\n${errors} physical-direction CSS declaration(s) found.`);
  process.exit(1);
}
console.log('RTL check passed: no physical left/right CSS found.');
