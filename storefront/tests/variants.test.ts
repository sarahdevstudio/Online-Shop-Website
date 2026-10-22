import { describe, expect, it } from 'vitest';
import { pickVariant, selectionFromParams, variantForOption, variantQuery, type Axis } from '../src/lib/variants';

const axes: Axis[] = [
  { code: 'wattage', options: [{ slug: '9w' }, { slug: '12w' }] },
  { code: 'cct', options: [{ slug: '3000k' }, { slug: '6500k' }] },
];

// 9W/3000K is sold out; 12W only exists in 6500K.
const variants = [
  { id: 1, inStock: false, attributes: { wattage: '9w', cct: '3000k' } },
  { id: 2, inStock: true, attributes: { wattage: '9w', cct: '6500k' } },
  { id: 3, inStock: true, attributes: { wattage: '12w', cct: '6500k' } },
];

describe('selectionFromParams', () => {
  it('keeps only axis codes with existing options, case-insensitively', () => {
    expect(selectionFromParams(axes, { wattage: '12W', cct: '9999k', utm_source: 'x', page: '2' })).toEqual({ wattage: '12w' });
    expect(selectionFromParams(axes, { wattage: ['9w', '12w'] })).toEqual({ wattage: '9w' });
  });
});

describe('pickVariant (preselection from the URL)', () => {
  it('preselects the exact match, even when it is out of stock', () => {
    expect(pickVariant(variants, { wattage: '9w', cct: '3000k' })?.id).toBe(1);
    expect(pickVariant(variants, { wattage: '12w', cct: '6500k' })?.id).toBe(3);
  });

  it('falls back to the best partial match for a combination that does not exist', () => {
    // 12W/3000K doesn't exist: 12W/6500K matches one value and is in stock.
    expect(pickVariant(variants, { wattage: '12w', cct: '3000k' })?.id).toBe(3);
  });

  it('prefers an in-stock variant among equal matches', () => {
    expect(pickVariant(variants, { wattage: '9w' })?.id).toBe(2);
  });

  it('defaults to the first in-stock variant without a selection', () => {
    expect(pickVariant(variants, {})?.id).toBe(2);
    expect(pickVariant([], {})).toBeNull();
  });
});

describe('variantForOption', () => {
  it('keeps the other selections when that combination exists', () => {
    expect(variantForOption(variants, variants[2], 'wattage', '9w')?.id).toBe(2); // 9W keeps 6500K
  });

  it('moves to an existing combination instead of a missing one', () => {
    expect(variantForOption(variants, variants[0], 'wattage', '12w')?.id).toBe(3); // 12W/3000K doesn't exist
  });

  it('returns null for an option no variant has', () => {
    expect(variantForOption(variants, variants[0], 'wattage', '20w')).toBeNull();
  });
});

describe('variantQuery', () => {
  it('builds the preselect URL in axis order', () => {
    expect(variantQuery(axes, variants[1])).toBe('?wattage=9w&cct=6500k');
    expect(variantQuery([], variants[1])).toBe('');
  });

  it('round-trips through the URL parser', () => {
    const query = Object.fromEntries(new URLSearchParams(variantQuery(axes, variants[0])));
    expect(pickVariant(variants, selectionFromParams(axes, query))?.id).toBe(1);
  });
});
