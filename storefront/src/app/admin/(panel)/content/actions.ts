'use server';

import { revalidatePath } from 'next/cache';
import { adminFetch } from '@/lib/admin/api';
import { failState, okState, problemState, submittedFrom, type FormState } from '@/lib/admin/form-state';
import { adminMessages } from '@/lib/admin/messages';
import { isInfoSlug } from '@/lib/content';

/**
 * Saves an info page. `rowVersion` is the version the editor was loaded with, so saving over someone else's newer edit is a
 * 409 "data changed". No step-up password: these are not payment settings.
 */
export async function saveContentAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const slug = String(formData.get('slug') ?? '');
  if (!isInfoSlug(slug)) return failState(adminMessages.notFound);

  const body = {
    title: String(formData.get('title') ?? ''),
    body: String(formData.get('body') ?? ''),
    rowVersion: String(formData.get('rowVersion') ?? ''),
  };
  const result = await adminFetch<unknown>('PUT', `/admin/content-pages/${slug}`, { body });
  if (!result.ok) return problemState(result.problem, submittedFrom(formData));

  revalidatePath(`/admin/content/${slug}`);
  revalidatePath('/admin/content');
  return okState(adminMessages.saved);
}
