'use server';

import { revalidatePath } from 'next/cache';
import { messages } from '@/lib/messages';
import { storeFetch } from '@/lib/store-api';
import type { OrderDto } from '@/lib/store-types';
import { INITIAL_TRANSFER_STATE, type TransferState } from '@/lib/transfer-form';

/** Saves the customer's transfer reference (tracking number, time, last 4 card digits). The order token is the credential. */
export async function submitTransferReferenceAction(_previous: TransferState, formData: FormData): Promise<TransferState> {
  const read = (name: string) => (typeof formData.get(name) === 'string' ? (formData.get(name) as string) : '');
  const token = read('token');
  const values = { tracking: read('tracking'), time: read('time'), cardLast4: read('cardLast4') };

  if (!/^[A-Za-z0-9_-]{43}$/.test(token)) return { ...INITIAL_TRANSFER_STATE, values, error: messages.transferFailed };

  const result = await storeFetch<OrderDto>('POST', `/orders/${token}/transfer-reference`, { body: values });
  if (result.ok) {
    revalidatePath(`/order/${token}`);
    return { saved: true, error: null, errors: {}, values };
  }

  const { problem } = result;
  if (problem.code === 'not_awaiting_payment') return { ...INITIAL_TRANSFER_STATE, values, error: messages.transferNotAllowed };
  if (problem.status === 400 && problem.errors) {
    const errors: Record<string, string> = {};
    for (const [key, list] of Object.entries(problem.errors)) {
      if (list.length > 0) errors[key.charAt(0).toLowerCase() + key.slice(1)] = list[0];
    }
    return { saved: false, error: messages.checkoutFixErrors, errors, values };
  }
  if (problem.status === 429) return { ...INITIAL_TRANSFER_STATE, values, error: messages.cartErrors.rate_limited };
  return { ...INITIAL_TRANSFER_STATE, values, error: messages.transferFailed };
}
