import { apiGet } from './api';

/** Customer-facing store settings from `GET /api/v1/catalog/store-info` (contact details, shipping, payment methods). */
export interface StoreInfo {
  storePhone: string;
  storeAddress: string;
  workingHours: string;
  instagramHandle: string;
  shippingFlatFee: number;
  freeShippingThreshold: number | null;
  cardToCard: boolean;
  cashOnDelivery: boolean;
}

/** Seconds the info stays in Next's data cache; an edit in the admin shows up within this time. */
const REVALIDATE_SECONDS = 60;

/** Footer data must never take a page down: any failure means "no contact details". */
export async function getStoreInfo(): Promise<StoreInfo | null> {
  try {
    return await apiGet<StoreInfo>('/catalog/store-info', REVALIDATE_SECONDS);
  } catch {
    return null;
  }
}
