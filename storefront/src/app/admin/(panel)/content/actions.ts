'use server';

import { revalidatePath } from 'next/cache';
import { adminFetch } from '@/lib/admin/api';
import { failState, okState, problemState, submittedFrom, type FormState } from '@/lib/admin/form-state';
import { adminMessages } from '@/lib/admin/messages';
import { id as readId } from '@/lib/admin/form-input';
import { applyPathChange, refreshSiteLinks } from '@/lib/admin/route-actions';
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
  const submitted = submittedFrom(formData);
  const pageId = readId(formData);
  if (pageId !== null) {
    // The URL first: a refused path stops here and nothing else is saved.
    const path = await applyPathChange('contentPage', pageId, formData, submitted);
    if (!path.ok) return path.state;
  }
  const result = await adminFetch<unknown>('PUT', `/admin/content-pages/${slug}`, { body });
  if (!result.ok) return problemState(result.problem, submitted);

  refreshSiteLinks(); // the draft marker may have been added or removed: links to this page appear or disappear
  revalidatePath(`/admin/content/${slug}`);
  revalidatePath('/admin/content');
  return okState(adminMessages.saved);
}
