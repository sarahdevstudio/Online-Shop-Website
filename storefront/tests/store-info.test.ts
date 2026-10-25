import { beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('next/server', () => ({ connection: vi.fn(async () => undefined) }));

const apiGet = vi.fn();
vi.mock('../src/lib/api', () => ({ apiGet }));

const { getStoreInfo } = await import('../src/lib/store-info');

describe('getStoreInfo', () => {
  beforeEach(() => {
    apiGet.mockReset(); // no return value: vitest would run a returned function as the teardown hook
  });

  it('reads the public store info with a short cache time', async () => {
    const info = { storePhone: '02112345678', storeAddress: '', workingHours: '', instagramHandle: 'shop', shippingFlatFee: 50000, freeShippingThreshold: null, cardToCard: true, cashOnDelivery: true };
    apiGet.mockResolvedValue(info);

    await expect(getStoreInfo()).resolves.toEqual(info);
    expect(apiGet).toHaveBeenCalledWith('/catalog/store-info', 60);
  });

  it('never takes a page down: a failing API means no contact details', async () => {
    apiGet.mockImplementation(() => {
      throw new Error('API down'); // thrown synchronously: avoids a dangling rejected promise in the mock's own bookkeeping
    });

    const result = await getStoreInfo();

    expect(result).toBeNull();
  });
});
