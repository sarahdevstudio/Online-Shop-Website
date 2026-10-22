import { describe, expect, it } from 'vitest';
import { formatNumber, formatToman } from '../src/lib/format';
import { IRR_PER_TOMAN, productJsonLd } from '../src/lib/seo';

describe('price formatting', () => {
  it('uses Persian digits and Persian thousands separators', () => {
    expect(formatNumber(1_250_000)).toBe('۱٬۲۵۰٬۰۰۰');
    expect(formatNumber(98_000)).toBe('۹۸٬۰۰۰');
    expect(formatNumber(500)).toBe('۵۰۰');
  });

  it('formats Toman amounts with the unit', () => {
    expect(formatToman(34_500_000)).toBe('۳۴٬۵۰۰٬۰۰۰ تومان');
  });
});

describe('productJsonLd', () => {
  const base = {
    name: 'لامپ LED حبابی پارس شهاب',
    description: 'توضیح',
    path: '/product/pars-shahab-led-bulb-e27',
    images: [],
    brand: 'پارس شهاب',
  };

  it('has one Offer per variant, in IRR (Toman × 10), with per-variant availability', () => {
    const ld = productJsonLd({
      ...base,
      offers: [
        { sku: 'PS-9W-3000', priceToman: 98_000, availability: 'InStock', path: '/product/x?wattage=9w&cct=3000k' },
        { sku: 'PS-9W-6500', priceToman: 98_000, availability: 'OutOfStock', path: '/product/x?wattage=9w&cct=6500k' },
      ],
    });

    expect(IRR_PER_TOMAN).toBe(10);
    expect(ld.offers).toHaveLength(2);
    expect(ld.offers?.[0]).toMatchObject({
      '@type': 'Offer',
      sku: 'PS-9W-3000',
      priceCurrency: 'IRR',
      price: 980_000,
      availability: 'https://schema.org/InStock',
    });
    expect(ld.offers?.[1].availability).toBe('https://schema.org/OutOfStock');
    expect(ld.offers?.[0].url).toMatch(/\/product\/x\?wattage=9w&cct=3000k$/);
    expect(ld.brand).toEqual({ '@type': 'Brand', name: 'پارس شهاب' });
  });

  it('marks archived products Discontinued', () => {
    const ld = productJsonLd({ ...base, offers: [{ sku: 'A', priceToman: 135_000, availability: 'Discontinued', path: base.path }] });

    expect(ld.offers?.[0].availability).toBe('https://schema.org/Discontinued');
  });

  it('never contains ratings or reviews', () => {
    const json = JSON.stringify(productJsonLd({ ...base, offers: [{ sku: 'A', priceToman: 1, availability: 'InStock', path: base.path }] }));

    expect(json).not.toMatch(/aggregateRating|AggregateRating|review/i);
  });
});
