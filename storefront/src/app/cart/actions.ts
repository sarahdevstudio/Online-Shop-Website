'use server';

import { cookies } from 'next/headers';
import { redirect } from 'next/navigation';
import { CART_COOKIE, cartCookieOptions } from '@/lib/cart-cookie';
import { cartErrorKey, parseQuantity, parseVariantId } from '@/lib/cart-input';
import { storeFetch } from '@/lib/store-api';
import type { CartMutationDto } from '@/lib/store-types';

/**
 * Cart mutations. Plain `<form action>` targets: they work without JavaScript (a normal POST, then a redirect), and Next
 * checks the request's Origin against the Host, which is the CSRF protection for the browser → Next hop.
 */

async function mutate(method: 'POST' | 'PUT' | 'DELETE', path: string, body?: unknown): Promise<never> {
  const jar = await cookies();
  const token = jar.get(CART_COOKIE)?.value;
  const result = await storeFetch<CartMutationDto>(method, path, { body, cartToken: token });

  if (!result.ok) redirect(`/cart?error=${cartErrorKey(result.status, result.problem.code)}`);

  // New cart → remember its token; existing cart → push the cookie's 30-day expiry forward.
  const active = result.body.newToken ?? token;
  if (active) jar.set(CART_COOKIE, active, cartCookieOptions());
  redirect(result.body.clamped ? '/cart?notice=clamped' : '/cart');
}

export async function addToCartAction(formData: FormData): Promise<void> {
  const variantId = parseVariantId(formData.get('variantId'));
  const quantity = parseQuantity(formData.get('quantity')) ?? 1;
  if (variantId === null) redirect('/cart?error=invalid');
  await mutate('POST', '/cart/items', { variantId, quantity });
}

export async function updateQuantityAction(formData: FormData): Promise<void> {
  const variantId = parseVariantId(formData.get('variantId'));
  const quantity = parseQuantity(formData.get('quantity'), true);
  if (variantId === null || quantity === null) redirect('/cart?error=invalid');
  await mutate('PUT', `/cart/items/${variantId}`, { quantity });
}

export async function removeItemAction(formData: FormData): Promise<void> {
  const variantId = parseVariantId(formData.get('variantId'));
  if (variantId === null) redirect('/cart?error=invalid');
  await mutate('DELETE', `/cart/items/${variantId}`);
}
