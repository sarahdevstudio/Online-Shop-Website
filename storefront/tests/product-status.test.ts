import { describe, expect, it } from 'vitest';
import { apiProductStatus, parseProductStatus, PRODUCT_STATUSES } from '@/lib/admin/product-status';

describe('admin product status filter', () => {
  it.each([
    ['draft', 'Draft'],
    ['published', 'Published'],
    ['archived', 'Archived'],
  ] as const)('sends %s to the API as %s', (status, expected) => {
    expect(apiProductStatus(status)).toBe(expected);
  });

  it('covers every status the page offers', () => {
    expect(PRODUCT_STATUSES.map(apiProductStatus)).toEqual(['Draft', 'Published', 'Archived']);
  });

  it('accepts known names and treats anything else as "all"', () => {
    for (const status of PRODUCT_STATUSES) expect(parseProductStatus(status)).toBe(status);
    for (const bad of [undefined, '', 'Published', 'bogus', '1']) expect(parseProductStatus(bad)).toBe('');
  });
});
