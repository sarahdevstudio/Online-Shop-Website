'use server';

import { revalidatePath } from 'next/cache';
import { adminFetch } from '@/lib/admin/api';
import { okState, problemState, submittedFrom, type FormState } from '@/lib/admin/form-state';
import { checked, text, whole } from '@/lib/admin/form-input';
import { adminMessages } from '@/lib/admin/messages';

/**
 * Saves the store settings. `currentPassword` goes along in the same request: the API requires it (step-up) only when the
 * card number, holder, bank or a payment-method switch changes, and answers 403 otherwise. `rowVersion` is the version the
 * form was loaded with, so a save over someone else's newer save is a 409 "data changed".
 */
export async function saveSettingsAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const body = {
    settings: {
      shippingFlatFee: whole(formData, 'shippingFlatFee') ?? -1,
      freeShippingThreshold: whole(formData, 'freeShippingThreshold'),
      cardToCardEnabled: checked(formData, 'cardToCardEnabled'),
      cashOnDeliveryEnabled: checked(formData, 'cashOnDeliveryEnabled'),
      cardNumber: text(formData, 'cardNumber'),
      cardHolderName: text(formData, 'cardHolderName'),
      bankName: text(formData, 'bankName'),
      maxQuantityPerLine: whole(formData, 'maxQuantityPerLine') ?? 0,
      storePhone: text(formData, 'storePhone'),
      storeAddress: text(formData, 'storeAddress'),
      workingHours: text(formData, 'workingHours'),
      instagramHandle: text(formData, 'instagramHandle'),
    },
    rowVersion: text(formData, 'rowVersion'),
    currentPassword: String(formData.get('currentPassword') ?? '') || null,
  };

  const result = await adminFetch<unknown>('PUT', '/admin/settings', { body });
  if (!result.ok) {
    const state = problemState(result.problem, submittedFrom(formData));
    // Field-level text for the password box when the step-up check failed.
    return result.problem.code === 'step_up_failed' ? { ...state, errors: { currentPassword: adminMessages.stepUpFailed } } : state;
  }
  revalidatePath('/admin/settings');
  return okState(adminMessages.saved);
}
