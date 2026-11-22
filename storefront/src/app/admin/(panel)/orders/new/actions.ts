'use server';

import { cookies } from 'next/headers';
import { adminFetch, adminGet } from '@/lib/admin/api';
import { adminMessages } from '@/lib/admin/messages';
import { DRAFT_CART_COOKIE, draftCartCookieOptions } from '@/lib/admin/draft-cart-cookie';
import type { AdminProduct, AdminProductListItem, PagedList } from '@/lib/admin/types';
import type { CartDto, CartMutationDto, PlacedOrderDto } from '@/lib/store-types';
import type { ComboOption } from '@/components/form';
import { formatNumber } from '@/lib/format';

/** One variant per option, so the admin picks an exact SKU; the product's name is its label. */
export async function searchVariantsAction(query: string): Promise<ComboOption[]> {
  const text = query.trim().slice(0, 60);
  if (!text) return [];
  const page = await adminGet<PagedList<AdminProductListItem>>(`/admin/products?q=${encodeURIComponent(text)}`);
  if (!page) return [];
  const products = await Promise.all(page.items.slice(0, 8).map((item) => adminGet<AdminProduct>(`/admin/products/${item.id}`)));
  const options: ComboOption[] = [];
  for (const product of products) {
    if (!product) continue;
    for (const variant of product.variants) {
      if (!variant.isActive) continue;
      options.push({
        value: String(variant.id),
        label: product.name,
        detail: adminMessages.orders.newOrder.variantDetail(variant.sku, formatNumber(variant.price), formatNumber(variant.stock)),
      });
    }
  }

  return options.slice(0, 20);
}

async function draftToken(): Promise<string | undefined> {
  return (await cookies()).get(DRAFT_CART_COOKIE)?.value;
}

async function rememberToken(newToken: string | null | undefined) {
  if (newToken) (await cookies()).set(DRAFT_CART_COOKIE, newToken, draftCartCookieOptions());
}

async function clearToken() {
  (await cookies()).delete(DRAFT_CART_COOKIE);
}

type Outcome<T> = { ok: true; value: T } | { ok: false; message: string };

function fail(detail: string | null | undefined): { ok: false; message: string } {
  return { ok: false, message: detail || adminMessages.orders.newOrder.failed };
}

export async function getDraftCartAction(): Promise<CartDto> {
  const token = await draftToken();
  const result = await adminFetch<CartDto>('GET', `/admin/orders/draft${token ? `?cartToken=${encodeURIComponent(token)}` : ''}`);
  return result.ok ? result.body : { lines: [], itemCount: 0, subtotal: 0, shippingFee: 0, total: 0, freeShippingThreshold: null, hasBlockingIssue: false, paymentMethods: { cardToCard: true, cashOnDelivery: true }, discount: null };
}

async function mutateDraft(method: 'POST' | 'PUT' | 'DELETE', path: string, body?: unknown): Promise<Outcome<CartDto>> {
  const token = await draftToken();
  const query = token ? `?cartToken=${encodeURIComponent(token)}` : '';
  const result = await adminFetch<CartMutationDto | CartDto>(method, `/admin/orders/draft${path}${query}`, { body });
  if (!result.ok) return fail(result.problem.detail ?? Object.values(result.problem.errors ?? {})[0]?.[0]);
  if ('newToken' in result.body) {
    await rememberToken(result.body.newToken);
    return { ok: true, value: result.body.cart };
  }

  return { ok: true, value: result.body };
}

export async function addDraftItemAction(variantId: number, quantity: number): Promise<Outcome<CartDto>> {
  return mutateDraft('POST', '/items', { variantId, quantity });
}

export async function setDraftQuantityAction(variantId: number, quantity: number): Promise<Outcome<CartDto>> {
  return mutateDraft('PUT', `/items/${variantId}`, { quantity });
}

export async function applyDraftDiscountAction(code: string): Promise<Outcome<CartDto>> {
  return mutateDraft('POST', '/discount', { code });
}

export async function removeDraftDiscountAction(): Promise<Outcome<CartDto>> {
  return mutateDraft('DELETE', '/discount');
}

export interface PlaceDraftOrderInput {
  fullName: string;
  mobile: string;
  province: string;
  city: string;
  address: string;
  postalCode: string;
  note: string;
  paymentMethod: 'cardToCard' | 'cashOnDelivery';
  expectedTotal: number;
}

export async function placeDraftOrderAction(input: PlaceDraftOrderInput): Promise<Outcome<PlacedOrderDto>> {
  const token = await draftToken();
  if (!token) return fail(adminMessages.orders.newOrder.cartEmpty);

  const idempotencyKey = crypto.randomUUID();
  const result = await adminFetch<PlacedOrderDto>(
    'POST',
    `/admin/orders?cartToken=${encodeURIComponent(token)}&idempotencyKey=${idempotencyKey}`,
    { body: { ...input, note: input.note || null } },
  );
  if (!result.ok) return fail(result.problem.detail ?? Object.values(result.problem.errors ?? {})[0]?.[0]);
  await clearToken();
  return { ok: true, value: result.body };
}
