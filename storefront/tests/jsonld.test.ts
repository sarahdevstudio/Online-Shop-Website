import { describe, expect, it } from 'vitest';
import { breadcrumbJsonLd, collectionPageJsonLd, contactJsonLd, organizationJsonLd, productJsonLd, websiteJsonLd } from '../src/lib/seo';
import { validateJsonLd } from './helpers/jsonld-shapes';

const valid = (doc: unknown) => expect(validateJsonLd(doc)).toEqual([]);

describe('JSON-LD of each page type matches the schema.org shapes', () => {
  it('Product with one Offer per variant (IRR, ItemAvailability URL, absolute URLs)', () => {
    valid(
      productJsonLd({
        name: 'لامپ',
        description: 'توضیح',
        path: '/product/led-9w',
        images: ['http://localhost:3000/media/a.webp'],
        brand: 'پارس شهاب',
        offers: [
          { sku: 'A1', priceToman: 98_000, availability: 'InStock', path: '/product/led-9w?wattage=9w' },
          { sku: 'A2', priceToman: 120_000, availability: 'OutOfStock', path: '/product/led-9w?wattage=12w' },
        ],
      }),
    );
  });

  it('Product offers carry shippingDetails only when a real shipping fee is known', () => {
    const withShipping = productJsonLd({
      name: 'لامپ',
      description: 'توضیح',
      path: '/product/led-9w',
      images: [],
      offers: [{ sku: 'A1', priceToman: 98_000, availability: 'InStock', path: '/product/led-9w' }],
      shippingFeeToman: 50_000,
    });
    valid(withShipping);
    expect(withShipping.offers![0].shippingDetails).toMatchObject({ '@type': 'OfferShippingDetails', shippingRate: { value: 500_000, currency: 'IRR' } });
    const withoutShipping = productJsonLd({ name: 'لامپ', description: 'توضیح', path: '/product/led-9w', images: [], offers: [{ sku: 'A1', priceToman: 98_000, availability: 'InStock', path: '/product/led-9w' }] });
    expect(withoutShipping.offers![0]).not.toHaveProperty('shippingDetails'); // not sourced from real data: omitted, never guessed
  });

  it('a product without any offer is not valid (nothing to show)', () => {
    expect(validateJsonLd(productJsonLd({ name: 'x', description: '', path: '/product/x', images: [], offers: [] }))).toContain('jsonld: needs offers, aggregateRating or review');
  });

  it('BreadcrumbList: positions 1..n, absolute URLs, only the last item may omit its URL', () => {
    valid(breadcrumbJsonLd([{ label: 'خانه', href: '/' }, { label: 'لامپ', href: '/category/lamps' }, { label: 'محصول' }]));
    const broken = { ...breadcrumbJsonLd([{ label: 'a' }, { label: 'b', href: '/b' }]) };
    expect(validateJsonLd(broken).join(' ')).toContain('only the last item may omit');
  });

  it('Organization, WebSite and the contact/home page (Store subtype, real hours only)', () => {
    valid(organizationJsonLd());
    expect(organizationJsonLd().logo).toMatch(/^https?:\/\/.+\/brand\/logo\.png$/); // the fallback logo, absolute
    valid(organizationJsonLd({ sameAs: ['https://www.instagram.com/shop/'] }));
    valid(organizationJsonLd({ logoUrl: '/media/site/logo.webp', sameAs: ['https://www.instagram.com/shop/'] }));
    expect(organizationJsonLd({ logoUrl: '/media/site/logo.webp' }).logo).toMatch(/^https?:\/\/.+\/media\/site\/logo\.webp$/);
    expect(organizationJsonLd({ sameAs: [] })).not.toHaveProperty('sameAs'); // no social links set: left out, not an empty array
    valid(websiteJsonLd());
    valid(contactJsonLd({ phone: '021-123', address: 'تهران', hours: '', instagram: 'shop' }));
    valid(contactJsonLd({ phone: '021-123', address: '', hours: '', instagram: '' }));

    const withAddress = contactJsonLd({ phone: '021-123', address: 'تهران، خیابان آزادی', hours: 'شنبه تا پنجشنبه ۹ تا ۱۸', instagram: 'shop' });
    expect(withAddress['@type']).toBe('Store'); // a LocalBusiness subtype, not plain Organization, once there is a real address
    expect(withAddress.openingHours).toBe('شنبه تا پنجشنبه ۹ تا ۱۸');
    valid(withAddress);
    // No address: no openingHours either (it would be meaningless without a place), and no invented geo ever appears.
    const noAddress = contactJsonLd({ phone: '021-123', address: '', hours: 'شنبه تا پنجشنبه ۹ تا ۱۸', instagram: '' });
    expect(noAddress).not.toHaveProperty('openingHours');
    expect(noAddress).not.toHaveProperty('geo');
  });

  it('CollectionPage/ItemList for a category listing: sequential positions, absolute URLs', () => {
    const doc = collectionPageJsonLd([{ name: 'لامپ ۹ وات', path: '/product/led-9w' }, { name: 'لامپ ۱۲ وات', path: '/product/led-12w' }]);
    valid(doc);
    expect(collectionPageJsonLd([])).toBeNull(); // an empty listing gets no ItemList at all
  });

  it('BlogPosting: headline/image/dates/author=Organization/publisher with logo', () => {
    const publisher = organizationJsonLd({ logoUrl: '/media/site/logo.webp' });
    const doc = {
      '@context': 'https://schema.org',
      '@type': 'BlogPosting',
      headline: 'راهنمای خرید لامپ',
      description: 'خلاصه',
      image: 'http://localhost:3000/media/site/share.webp',
      datePublished: '2026-10-01T00:00:00Z',
      dateModified: '2026-10-02T00:00:00Z',
      inLanguage: 'fa-IR',
      mainEntityOfPage: 'http://localhost:3000/blog/guide',
      author: publisher,
      publisher,
    };
    valid(doc);
    expect(doc.author['@type']).toBe('Organization');
    expect(doc.publisher.logo).toMatch(/^https?:\/\//);
  });

  it('the validator really rejects bad data', () => {
    const offer = (over: Record<string, unknown>) => ({ '@context': 'https://schema.org', '@type': 'Product', name: 'x', offers: [{ '@type': 'Offer', price: 1, priceCurrency: 'IRR', availability: 'https://schema.org/InStock', ...over }] });
    expect(validateJsonLd(offer({ priceCurrency: 'Toman' }))).not.toEqual([]);
    expect(validateJsonLd(offer({ availability: 'InStock' }))).not.toEqual([]);
    expect(validateJsonLd(offer({ price: -5 }))).not.toEqual([]);
    expect(validateJsonLd({ '@type': 'Product', name: 'x' })).toContain('jsonld: @context must be https://schema.org');
    expect(validateJsonLd({ '@context': 'https://schema.org', '@type': 'Mystery' })[0]).toContain('no validator');
  });
});

import { faqJsonLd, withSeoOverrides } from '../src/lib/seo';
import { EMPTY_SEO } from '../src/lib/entity-seo';

describe('FAQ structured data and SEO overrides', () => {
  it('FAQPage has one Question with an acceptedAnswer per item, and none at all without items', () => {
    valid(faqJsonLd([{ question: 'ضمانت دارد؟', answer: 'بله.' }, { question: 'سرپیچ؟', answer: 'E27' }]));
    expect(faqJsonLd([])).toBeNull();
    const broken = { '@context': 'https://schema.org', '@type': 'FAQPage', mainEntity: [{ '@type': 'Question', name: 'q' }] };
    expect(validateJsonLd(broken).join(' ')).toContain('acceptedAnswer is required');
  });

  it('a filled override replaces the generated value; empty ones change nothing; noindex keeps links followed', () => {
    const base = { title: 'عنوان خودکار', description: 'توضیح خودکار', image: 'http://x/default.png' };
    expect(withSeoOverrides(base, EMPTY_SEO)).toEqual({ ...base, noindex: undefined });
    const over = withSeoOverrides(base, { ...EMPTY_SEO, title: 'عنوان سئو', description: 'توضیح سئو', ogImageUrl: '/media/site/a.png', noIndex: true });
    expect(over).toMatchObject({ title: 'عنوان سئو', description: 'توضیح سئو', image: 'http://localhost:3000/media/site/a.png', noindex: 'follow' });
    expect(withSeoOverrides({ ...base, noindex: true as const }, { ...EMPTY_SEO, noIndex: true }).noindex).toBe(true); // an already stricter page stays stricter
  });
});
