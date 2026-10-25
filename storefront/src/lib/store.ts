import { cookies } from 'next/headers';
import { CART_COOKIE } from './cart-cookie';
import { storeFetch } from './store-api';
import type { CartDto, OrderDto } from './store-types';

/** Reads for Server Components. Mutations live in the route's `actions.ts`. */

export async function getCartToken(): Promise<string | undefined> {
  return (await cookies()).get(CART_COOKIE)?.value;
}

const EMPTY_CART: CartDto = {
  lines: [],
  itemCount: 0,
  subtotal: 0,
  shippingFee: 0,
  total: 0,
  freeShippingThreshold: null,
  hasBlockingIssue: false,
  paymentMethods: { cardToCard: true, cashOnDelivery: true },
};

/** The live cart with current prices. No cookie = no API call. API failures propagate (error page). */
export async function getCart(): Promise<CartDto> {
  const token = await getCartToken();
  if (!token) return EMPTY_CART;
  const result = await storeFetch<CartDto>('GET', '/cart', { cartToken: token });
  if (!result.ok) throw new Error(`GET /cart returned ${result.status}`);
  return result.body;
}

/** Header badge: must never break a page, so any failure counts as 0. */
export async function getCartCount(): Promise<number> {
  const token = await getCartToken();
  if (!token) return 0;
  try {
    const result = await storeFetch<{ count: number }>('GET', '/cart/count', { cartToken: token });
    return result.ok ? result.body.count : 0;
  } catch {
    return 0;
  }
}

/** The guest order page; null for an unknown token. */
export async function getOrder(accessToken: string): Promise<OrderDto | null> {
  if (!/^[A-Za-z0-9_-]{43}$/.test(accessToken)) return null;
  const result = await storeFetch<OrderDto>('GET', `/orders/${accessToken}`);
  if (result.ok) return result.body;
  if (result.status === 404) return null;
  throw new Error(`GET /orders/*** returned ${result.status}`);
}
