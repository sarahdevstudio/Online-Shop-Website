// Ported from the retired Angular check-rtl tool: CSS must use logical properties so the
// right-to-left layout cannot silently break. A line that is deliberately physical (a mirrored
// background-position, for instance) carries `/* rtl-ok */`.
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';

const FORBIDDEN: [RegExp, string][] = [
  [/(^|[\s;{])(margin|padding)-(left|right)\s*:/, 'use margin-inline-* / padding-inline-*'],
  [/(^|[\s;{])border-(left|right)(-(width|style|color))?\s*:/, 'use border-inline-start/end'],
  [/(^|[\s;{])border-(top|bottom)-(left|right)-radius\s*:/, 'use border-start-start-radius etc.'],
  [/(^|[\s;{])(left|right)\s*:/, 'use inset-inline-start / inset-inline-end'],
  [/(^|[\s;{])float\s*:\s*(left|right)/, 'avoid float; use flex/grid'],
  [/(^|[\s;{])clear\s*:\s*(left|right)/, 'avoid clear'],
  [/text-align\s*:\s*(left|right)/, 'use text-align: start | end'],
];

function* cssFiles(dir: string): Generator<string> {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) yield* cssFiles(full);
    else if (name.endsWith('.css')) yield full;
  }
}

describe('right-to-left safety', () => {
  it('no stylesheet uses physical left/right properties', () => {
    const found: string[] = [];
    for (const file of cssFiles(join(__dirname, '..', 'src'))) {
      readFileSync(file, 'utf8').split('\n').forEach((line, i) => {
        if (line.includes('rtl-ok')) return;
        for (const [pattern, hint] of FORBIDDEN) {
          if (pattern.test(line)) found.push(`${file}:${i + 1}  ${line.trim()}  → ${hint}`);
        }
      });
    }
    expect(found).toEqual([]);
  });
});
