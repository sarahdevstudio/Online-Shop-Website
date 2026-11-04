import fs from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';
import { RESERVED_EXACT, RESERVED_SEGMENTS, isReservedPath } from '../src/lib/reserved-paths';

const csharp = fs.readFileSync(path.resolve(__dirname, '..', '..', 'src', 'Domain', 'Routing', 'ReservedPaths.cs'), 'utf8');

/** The quoted strings of one list in ReservedPaths.cs. */
function listFromApi(name: 'Segments' | 'Exact'): string[] {
  const block = new RegExp(`${name} \\{ get; \\} =\\s*\\[([\\s\\S]*?)\\];`).exec(csharp)?.[1] ?? '';
  return [...block.matchAll(/"([^"]+)"/g)].map((m) => m[1]);
}

describe('reserved paths', () => {
  it('the proxy copy equals the API list (the API is the source of truth)', () => {
    expect([...RESERVED_SEGMENTS].sort()).toEqual(listFromApi('Segments').sort());
    expect([...RESERVED_EXACT].sort()).toEqual(listFromApi('Exact').sort());
  });

  it('matches by first segment, case-insensitively, with the blog index exact', () => {
    expect(isReservedPath('/cart')).toBe(true);
    expect(isReservedPath('/Order/abc')).toBe(true);
    expect(isReservedPath('/blog')).toBe(true);
    expect(isReservedPath('/blog/a-post')).toBe(false);
    expect(isReservedPath('/category/x')).toBe(false);
    expect(isReservedPath('/searching')).toBe(false);
    expect(isReservedPath('/')).toBe(false);
  });
});
