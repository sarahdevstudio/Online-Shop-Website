'use server';

import { revalidatePath } from 'next/cache';
import { adminFetch } from '@/lib/admin/api';
import { failState, okState, problemState, submittedFrom, type FormState } from '@/lib/admin/form-state';
import { checked, text, whole } from '@/lib/admin/form-input';
import { adminMessages } from '@/lib/admin/messages';
import { refreshCatalog, refreshPublicPaths } from '@/lib/admin/route-actions';
import { seoMessages } from '@/lib/admin/seo-messages';
import type { SeoTargetType } from '@/lib/admin/seo-types';

const TYPES: readonly SeoTargetType[] = ['product', 'category', 'contentPage', 'blogPost'];

/**
 * Saves the SEO panel of one entity. The entity's own permission is checked by the API; `rowVersion` is the version the panel was
 * loaded with (a save over a newer one is a 409). The shop's cached copies are dropped so the change is live at once.
 */
export async function saveSeoAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const type = text(formData, 'type') as SeoTargetType;
  const id = whole(formData, 'id');
  if (!TYPES.includes(type) || id === null || id <= 0) return failState(adminMessages.failed);

  const questions = formData.getAll('faqQuestion').map(String);
  const answers = formData.getAll('faqAnswer').map(String);
  const faq = questions.map((question, i) => ({ question: question.trim(), answer: (answers[i] ?? '').trim() })).filter((item) => item.question || item.answer);
  const submitted = submittedFrom(formData);
  const result = await adminFetch<unknown>('PUT', `/admin/seo/${type}/${id}`, {
    body: {
      metaTitle: text(formData, 'metaTitle') || null,
      metaDescription: text(formData, 'metaDescription') || null,
      ogMediaId: whole(formData, 'ogMediaId'),
      noIndex: checked(formData, 'noIndex'),
      faq: type === 'product' || type === 'category' ? faq : null,
      intro: type === 'category' ? text(formData, 'intro') || null : null,
      rowVersion: text(formData, 'rowVersion') || null,
    },
  });
  if (!result.ok) return problemState(result.problem, { ...submitted, faq: JSON.stringify(faq) });
  refreshCatalog();
  refreshPublicPaths();
  revalidatePath(type === 'product' ? `/admin/products/${id}` : type === 'category' ? '/admin/categories' : type === 'blogPost' ? `/admin/blog/${id}` : '/admin/content');
  return okState(seoMessages.saved);
}
