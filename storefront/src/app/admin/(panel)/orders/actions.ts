'use server';

import { revalidatePath } from 'next/cache';
import { adminFetch } from '@/lib/admin/api';
import { failState, okState, problemState, submittedFrom, type FormState } from '@/lib/admin/form-state';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminOrder } from '@/lib/admin/types';

function orderId(formData: FormData): number | null {
  const id = Number(formData.get('id'));
  return Number.isSafeInteger(id) && id > 0 ? id : null;
}

async function post(id: number, action: string, body: unknown, submitted: Record<string, string>): Promise<FormState> {
  const result = await adminFetch<unknown>('POST', `/admin/orders/${id}/${action}`, { body });
  if (!result.ok) return problemState(result.problem, submitted);
  revalidatePath(`/admin/orders/${id}`);
  return okState(adminMessages.orders.done);
}

export async function confirmPaymentAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const id = orderId(formData);
  return id === null ? failState(adminMessages.failed) : post(id, 'confirm-payment', undefined, submittedFrom(formData));
}

export async function advanceStatusAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const id = orderId(formData);
  const status = String(formData.get('status') ?? '');
  if (id === null || !['confirmed', 'shipped', 'delivered'].includes(status)) return failState(adminMessages.failed);
  return post(id, 'status', { status }, submittedFrom(formData));
}

/** Cancelling a PAID order needs the explicit acknowledgement, enforced here and not only in the browser. */
export async function cancelOrderAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const id = orderId(formData);
  if (id === null) return failState(adminMessages.failed);

  const current = await adminFetch<AdminOrder>('GET', `/admin/orders/${id}`);
  if (!current.ok) return problemState(current.problem);
  if (current.body.order.paymentStatus === 'paid' && formData.get('acknowledge') !== 'on') {
    return failState(adminMessages.orders.mustAcknowledge);
  }
  return post(id, 'cancel', undefined, submittedFrom(formData));
}
