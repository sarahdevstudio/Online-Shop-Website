'use server';

import { revalidatePath } from 'next/cache';
import { adminFetch } from '@/lib/admin/api';
import { failState, okState, problemState, submittedFrom, type FormState } from '@/lib/admin/form-state';
import { checked, id as readId, text } from '@/lib/admin/form-input';
import { adminMessages } from '@/lib/admin/messages';

export async function saveBrandAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const id = readId(formData);
  const submitted = submittedFrom(formData);
  const body = { name: text(formData, 'name'), slug: id === null ? text(formData, 'slug') : null, isActive: checked(formData, 'isActive') };
  const result = await adminFetch<unknown>(id === null ? 'POST' : 'PUT', id === null ? '/admin/brands' : `/admin/brands/${id}`, { body });
  if (!result.ok) return problemState(result.problem, submitted);
  revalidatePath('/admin/brands');
  return okState(adminMessages.saved);
}

export async function deleteBrandAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const id = readId(formData);
  if (id === null) return failState(adminMessages.failed);
  const result = await adminFetch<unknown>('DELETE', `/admin/brands/${id}`);
  if (!result.ok) return problemState(result.problem);
  revalidatePath('/admin/brands');
  return okState(adminMessages.taxonomy.deleted);
}
