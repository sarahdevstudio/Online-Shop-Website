import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('next/server', () => ({ connection: vi.fn(async () => undefined) }));

const { ApiUnavailableError, apiGet } = await import('../src/lib/api');
const { parseUtc, productListPath, toProductCard, toProductPage } = await import('../src/lib/catalog');

describe('toProductCard', () => {
  it('maps the API card DTO onto the card model', () => {
    expect(
      toProductCard({
        slug: 'zar-led-bulb-12w-e27',
        name: 'لامپ LED حبابی ۱۲ وات زر',
        brand: 'زر',
        price: 115_000,
        compareAtPrice: 135_000,
        inStock: true,
        image: { url: '/media/products/2/a.png', alt: 'لامپ' },
      }),
    ).toEqual({
      slug: 'zar-led-bulb-12w-e27',
      name: 'لامپ LED حبابی ۱۲ وات زر',
      brand: 'زر',
      price: 115_000,
      compareAtPrice: 135_000,
      inStock: true,
      imageUrl: '/media/products/2/a.png',
      imageAlt: 'لامپ',
    });
  });

  it('uses no image and the name as alt when the product has no image', () => {
    const card = toProductCard({ slug: 's', name: 'نام', brand: null, price: 1, compareAtPrice: null, inStock: false, image: null });
    expect(card.imageUrl).toBeNull();
    expect(card.imageAlt).toBe('نام');
    expect(card.brand).toBeNull();
  });
});

describe('toProductPage', () => {
  it('derives the page count from total and page size; an empty listing still has one page', () => {
    expect(toProductPage({ items: [], page: 1, pageSize: 24, total: 49 }).pageCount).toBe(3);
    expect(toProductPage({ items: [], page: 1, pageSize: 24, total: 0 }).pageCount).toBe(1);
  });
});

describe('productListPath', () => {
  it('sends only what is set, cuts q to the API limit and omits page 1', () => {
    expect(productListPath({ category: 'led-bulbs' })).toBe('/catalog/products?category=led-bulbs&pageSize=24');
    expect(productListPath({ inStock: true, pageSize: 8, page: 2 })).toBe('/catalog/products?inStock=true&page=2&pageSize=8');
    const long = productListPath({ q: `  ${'ل'.repeat(150)} ` });
    expect(decodeURIComponent(new URLSearchParams(long.split('?')[1]).get('q') ?? '').length).toBe(100);
  });
});

describe('parseUtc', () => {
  it('reads timestamps without a zone designator as UTC', () => {
    expect(parseUtc('2026-10-22T09:20:51.123')?.toISOString()).toBe('2026-10-22T09:20:51.123Z');
    expect(parseUtc('2026-10-22T09:20:51Z')?.toISOString()).toBe('2026-10-22T09:20:51.000Z');
    expect(parseUtc('2026-10-22T12:50:51+03:30')?.toISOString()).toBe('2026-10-22T09:20:51.000Z');
    expect(parseUtc(null)).toBeNull();
    expect(parseUtc('not a date')).toBeNull();
  });
});

describe('apiGet', () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    vi.stubGlobal('fetch', fetchMock);
    vi.stubEnv('API_BASE_URL', 'http://api.internal:8080/');
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.unstubAllEnvs();
    fetchMock.mockReset();
  });

  it('calls the server-only base URL with the given revalidation and returns the JSON', async () => {
    fetchMock.mockResolvedValue(new Response(JSON.stringify([{ slug: 'lamps' }]), { status: 200 }));

    await expect(apiGet('/catalog/categories', 300)).resolves.toEqual([{ slug: 'lamps' }]);
    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('http://api.internal:8080/api/v1/catalog/categories');
    expect(init.next).toEqual({ revalidate: 300 });
  });

  it('returns null only for a real 404', async () => {
    fetchMock.mockResolvedValue(new Response('{}', { status: 404 }));

    await expect(apiGet('/catalog/products/nope', 60)).resolves.toBeNull();
  });

  it.each([500, 502, 503])('treats %i as API unavailable', async (status) => {
    fetchMock.mockResolvedValue(new Response('{}', { status }));

    await expect(apiGet('/catalog/products/x', 60)).rejects.toBeInstanceOf(ApiUnavailableError);
  });

  it('treats a network error or timeout as API unavailable', async () => {
    fetchMock.mockRejectedValue(new TypeError('fetch failed'));

    await expect(apiGet('/catalog/categories', 300)).rejects.toBeInstanceOf(ApiUnavailableError);
  });

  it('treats another 4xx as a client bug, not as unavailable or not found', async () => {
    fetchMock.mockResolvedValue(new Response('{}', { status: 400 }));

    const error = await apiGet('/catalog/products?sort=x', 60).catch((e: unknown) => e);
    expect(error).toBeInstanceOf(Error);
    expect(error).not.toBeInstanceOf(ApiUnavailableError);
  });

  it('refuses to guess the API in production when API_BASE_URL is missing', async () => {
    vi.stubEnv('API_BASE_URL', '');
    vi.stubEnv('NODE_ENV', 'production');

    await expect(apiGet('/catalog/categories', 300)).rejects.toBeInstanceOf(ApiUnavailableError);
    expect(fetchMock).not.toHaveBeenCalled();
  });
});
