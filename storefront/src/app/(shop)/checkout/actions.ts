'use server';

import { cookies } from 'next/headers';
import { redirect } from 'next/navigation';
import { CART_COOKIE } from '@/lib/cart-cookie';
import { isIdempotencyKey, mapApiErrors, readCheckoutValues, type CheckoutState } from '@/lib/checkout-form';
import { messages } from '@/lib/messages';
import { persianOr } from '@/lib/persian-only';
import { storeFetch } from '@/lib/store-api';
import type { PlacedOrderDto } from '@/lib/store-types';

/**
 * Places the order. Used through `useActionState`, which keeps working without JavaScript: the form is a normal POST and
 * the server renders the page again with the returned state. Success redirects to the guest order page.
 */
export async function placeOrderAction(_previous: CheckoutState, formData: FormData): Promise<CheckoutState> {
  const values = readCheckoutValues(formData);
  const idempotencyKey = formData.get('idempotencyKey');
  const expectedTotal = Number(formData.get('expectedTotal'));
  const fail = (formError: string, errors: Record<string, string> = {}): CheckoutState => ({ values, errors, formError });

  if (!isIdempotencyKey(idempotencyKey) || !Number.isSafeInteger(expectedTotal) || expectedTotal < 0) {
    return fail(messages.checkoutFailed);
  }
  if (values.paymentMethod !== 'cardToCard' && values.paymentMethod !== 'cashOnDelivery') {
    return fail(messages.checkoutFixErrors, { paymentMethod: 'روش پرداخت را انتخاب کنید.' });
  }

  const jar = await cookies();
  const result = await storeFetch<PlacedOrderDto>('POST', '/orders', {
    cartToken: jar.get(CART_COOKIE)?.value,
    idempotencyKey,
    body: {
      fullName: values.fullName,
      mobile: values.mobile,
      province: values.province,
      city: values.city,
      address: values.address,
      postalCode: values.postalCode,
      note: values.note.trim() === '' ? null : values.note,
      paymentMethod: values.paymentMethod,
      expectedTotal,
    },
  });

  if (result.ok) {
    jar.delete(CART_COOKIE); // the server-side cart is gone; a replay (double submit) still resolves by key
    redirect(`/order/${result.body.accessToken}`);
  }

  const { problem } = result;
  switch (problem.code) {
    case 'cart_empty':
      redirect('/cart?error=cart_empty');
    case 'price_changed':
      redirect('/cart?error=price_changed');
    case 'stock_insufficient': {
      const ids = (problem.lines ?? []).map((line) => line.variantId).join(',');
      redirect(`/cart?error=stock_insufficient&short=${ids}`);
    }
    case 'unpaid_limit':
      return fail(persianOr(problem.detail, messages.checkoutFailed));
    default:
      if (problem.status === 400 && problem.errors) return fail(messages.checkoutFixErrors, mapApiErrors(problem.errors));
      if (problem.status === 429) return fail(messages.cartErrors.rate_limited);
      return fail(messages.checkoutFailed);
  }
}
