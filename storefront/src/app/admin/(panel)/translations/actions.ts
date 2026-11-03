'use server';

import { revalidatePath } from 'next/cache';
import { adminFetch } from '@/lib/admin/api';
import { failState, okState, problemState, submittedFrom, type FormState } from '@/lib/admin/form-state';
import { adminMessages } from '@/lib/admin/messages';
import { refreshAllTexts } from '@/lib/i18n/admin-texts';

/**
 * Saves or resets one text. `rowVersion` is the version the editor loaded, so saving over someone else's newer edit is a 409
 * "data changed". The API checks that the text keeps exactly the placeholders of the shipped one; its message is shown under the field.
 */
export async function saveTranslationAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const id = Number(formData.get('id'));
  if (!Number.isSafeInteger(id) || id <= 0) return failState(adminMessages.notFound);
  const rowVersion = String(formData.get('rowVersion') ?? '');
  const reset = formData.get('intent') === 'reset';

  const result = reset
    ? await adminFetch<unknown>('POST', `/admin/translations/${id}/reset`, { body: { rowVersion } })
    : await adminFetch<unknown>('PUT', `/admin/translations/${id}`, { body: { text: String(formData.get('text') ?? ''), rowVersion } });
  if (!result.ok) return problemState(result.problem, submittedFrom(formData));

  await refreshAllTexts();
  revalidatePath(`/admin/translations/${id}`);
  revalidatePath('/admin/translations');
  revalidatePath('/', 'layout');
  return okState(reset ? adminMessages.translations.resetDone : adminMessages.translations.saved);
}
