'use server';

import { revalidatePath } from 'next/cache';
import { INITIAL_REVIEW_STATE, type ReviewFormState } from '@/lib/review-form';
import { reviewMessages as t } from '@/lib/review-messages';
import { storeFetch } from '@/lib/store-api';

const SLUG = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

/**
 * A guest's review of a product. It goes to the API with the storefront key and the visitor's own address (the API rate-limits
 * per address), is held for moderation, and nothing becomes public until then. The honeypot field `website` is passed on: a
 * bot that fills it is thanked and ignored by the API.
 */
export async function submitReviewAction(_previous: ReviewFormState, formData: FormData): Promise<ReviewFormState> {
  const read = (name: string) => String(formData.get(name) ?? '');
  const values = { name: read('name'), mobile: read('mobile'), rating: read('rating'), text: read('text') };
  const slug = read('productSlug');
  const fail = (message: string, errors: Record<string, string> = {}): ReviewFormState => ({ ok: false, message, errors, values });
  if (!SLUG.test(slug)) return fail(t.failed);
  const rating = Number.parseInt(values.rating, 10);

  const result = await storeFetch<unknown>('POST', '/reviews', { body: { productSlug: slug, name: values.name, mobile: values.mobile, rating: Number.isInteger(rating) ? rating : 0, text: values.text, website: read('website') } });
  if (result.ok) {
    revalidatePath('/', 'layout'); // a held review changes nothing public, but the form must start empty
    return { ok: true, message: t.thanks, errors: {}, values: INITIAL_REVIEW_STATE.values };
  }
  const { problem } = result;
  if (problem.status === 429) return fail(t.tooMany);
  if (problem.status === 400 && problem.errors && Object.keys(problem.errors).length > 0) {
    const errors: Record<string, string> = {};
    for (const [key, list] of Object.entries(problem.errors)) if (list[0]) errors[key.charAt(0).toLowerCase() + key.slice(1)] = list[0];
    return fail(t.fixErrors, errors);
  }
  return fail(problem.detail || t.failed);
}
