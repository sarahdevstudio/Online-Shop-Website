'use server';

import { revalidatePath } from 'next/cache';
import { adminFetch } from '@/lib/admin/api';
import { failState, okState, problemState, submittedFrom, type FormState } from '@/lib/admin/form-state';
import { checked, id as readId, text, whole } from '@/lib/admin/form-input';
import { adminMessages } from '@/lib/admin/messages';

export async function saveCategoryAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const id = readId(formData);
  const submitted = submittedFrom(formData);
  const body = {
    name: text(formData, 'name'),
    slug: id === null ? text(formData, 'slug') : null, // the slug is set on create only (immutable)
    parentId: whole(formData, 'parentId'),
    description: text(formData, 'description') || null,
    sortOrder: whole(formData, 'sortOrder') ?? 0,
    isActive: checked(formData, 'isActive'),
  };
  const result = await adminFetch<unknown>(id === null ? 'POST' : 'PUT', id === null ? '/admin/categories' : `/admin/categories/${id}`, { body });
  if (!result.ok) return problemState(result.problem, submitted);
  revalidatePath('/admin/categories');
  return okState(adminMessages.saved);
}

export async function deleteCategoryAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const id = readId(formData);
  if (id === null) return failState(adminMessages.failed);
  const result = await adminFetch<unknown>('DELETE', `/admin/categories/${id}`);
  if (!result.ok) return problemState(result.problem);
  revalidatePath('/admin/categories');
  return okState(adminMessages.taxonomy.deleted);
}
