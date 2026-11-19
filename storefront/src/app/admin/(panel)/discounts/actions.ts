'use server';

import { revalidatePath } from 'next/cache';
import { redirect } from 'next/navigation';
import { adminFetch } from '@/lib/admin/api';
import { checked, id as readId, text, whole } from '@/lib/admin/form-input';
import { failState, okState, problemState, submittedFrom, type FormState } from '@/lib/admin/form-state';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminDiscount } from '@/lib/admin/types';

/**
 * Creates (no id) or saves a discount code. `rowVersion` is the version the editor was loaded with, so saving over
 * someone else's newer edit is a 409 "data changed". The code itself is only sent on create and never changes.
 */
export async function saveDiscountAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const discountId = readId(formData);
  const submitted = submittedFrom(formData);
  const expiresAt = text(formData, 'expiresAt');
  const content = {
    type: text(formData, 'type'),
    value: whole(formData, 'value') ?? 0,
    minSubtotal: whole(formData, 'minSubtotal'),
    usageLimit: whole(formData, 'usageLimit'),
    perMobileLimit: whole(formData, 'perMobileLimit') ?? 1,
    expiresAtUtc: expiresAt ? `${expiresAt}T00:00:00Z` : null,
    isActive: checked(formData, 'isActive'),
  };

  if (discountId === null) {
    const result = await adminFetch<AdminDiscount>('POST', '/admin/discounts', { body: { ...content, code: text(formData, 'code') } });
    if (!result.ok) return problemState(result.problem, submitted);
    revalidatePath('/admin/discounts');
    redirect(`/admin/discounts/${result.body.id}`);
  }

  const result = await adminFetch<AdminDiscount>('PUT', `/admin/discounts/${discountId}`, {
    body: { ...content, rowVersion: String(formData.get('rowVersion') ?? '') },
  });
  if (!result.ok) return problemState(result.problem, submitted);
  revalidatePath('/admin/discounts');
  revalidatePath(`/admin/discounts/${discountId}`);
  return okState(adminMessages.saved);
}

export async function deleteDiscountAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const discountId = readId(formData);
  if (discountId === null) return failState(adminMessages.failed);
  const result = await adminFetch<unknown>('DELETE', `/admin/discounts/${discountId}`);
  if (!result.ok) return problemState(result.problem);
  revalidatePath('/admin/discounts');
  redirect('/admin/discounts');
}
