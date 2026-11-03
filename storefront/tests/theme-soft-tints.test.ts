import { describe, expect, it } from 'vitest';
import { contrastRatio } from '../src/lib/contrast';
import { themeTokens } from '../src/lib/theme-css';

describe('soft tints', () => {
  it('keep every status color readable (4.5:1) on its own soft background, also for the seeded palette', () => {
    const tokens = themeTokens({ primary: '#5b0057', success: '#1b7f3b', warning: '#8a5a00', danger: '#b3261e' });
    for (const name of ['primary', 'success', 'warning', 'danger']) {
      expect(contrastRatio(tokens[`--color-${name}`], tokens[`--color-${name}-soft`])).toBeGreaterThanOrEqual(4.5);
    }
  });
});
