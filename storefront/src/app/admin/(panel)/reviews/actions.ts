'use server';

import { revalidatePath } from 'next/cache';
import { adminFetch } from '@/lib/admin/api';
import { failState, okState, problemState, type FormState } from '@/lib/admin/form-state';
import { whole, text } from '@/lib/admin/form-input';
import { adminMessages } from '@/lib/admin/messages';
import { adminReviewMessages as t } from '@/lib/admin/review-messages';
import { refreshCatalog } from '@/lib/admin/route-actions';

/** Approve or reject one review (`decision` = approve | reject). The API checks Reviews.Manage and the row version (409 when someone decided meanwhile). */
export async function moderateReviewAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const id = whole(formData, 'id');
  const decision = text(formData, 'decision');
  if (id === null || id <= 0 || (decision !== 'approve' && decision !== 'reject')) return failState(adminMessages.failed);
  const result = await adminFetch<unknown>('POST', `/admin/reviews/${id}/${decision}`, { body: { rowVersion: text(formData, 'rowVersion') } });
  if (!result.ok) return problemState(result.problem);
  refreshCatalog(); // the product page shows (or stops showing) the review at once
  revalidatePath('/admin/reviews');
  return okState(decision === 'approve' ? t.approved : t.rejected);
}
