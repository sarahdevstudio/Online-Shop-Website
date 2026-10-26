'use server';

import { revalidatePath } from 'next/cache';
import { redirect } from 'next/navigation';
import { adminFetch } from '@/lib/admin/api';
import { checked, id as readId, text } from '@/lib/admin/form-input';
import { failState, okState, problemState, submittedFrom, type FormState } from '@/lib/admin/form-state';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminBlogPost } from '@/lib/admin/types';

/** What the storefront shows is cached for up to a minute; this refreshes the route caches right away. */
function refreshPublicPages(slug: string) {
  revalidatePath('/blog');
  revalidatePath(`/blog/${slug}`);
  revalidatePath('/sitemap.xml');
}

/**
 * Creates (no id) or saves a post. `rowVersion` is the version the editor was loaded with, so saving over someone else's newer
 * edit is a 409 "data changed". The slug is only sent on create and never changes afterwards.
 */
export async function saveBlogAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const postId = readId(formData);
  const submitted = submittedFrom(formData);
  const content = {
    title: text(formData, 'title'),
    excerpt: text(formData, 'excerpt'),
    body: String(formData.get('body') ?? ''),
    publish: checked(formData, 'publish'),
  };

  if (postId === null) {
    const result = await adminFetch<AdminBlogPost>('POST', '/admin/blog', { body: { ...content, slug: text(formData, 'slug') } });
    if (!result.ok) return problemState(result.problem, submitted);
    refreshPublicPages(result.body.slug);
    revalidatePath('/admin/blog');
    redirect(`/admin/blog/${result.body.id}`);
  }

  const result = await adminFetch<AdminBlogPost>('PUT', `/admin/blog/${postId}`, {
    body: { ...content, rowVersion: String(formData.get('rowVersion') ?? '') },
  });
  if (!result.ok) return problemState(result.problem, submitted);
  refreshPublicPages(result.body.slug);
  revalidatePath('/admin/blog');
  revalidatePath(`/admin/blog/${postId}`);
  return okState(adminMessages.saved);
}

export async function deleteBlogAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const postId = readId(formData);
  const slug = text(formData, 'slug');
  if (postId === null) return failState(adminMessages.failed);
  const result = await adminFetch<unknown>('DELETE', `/admin/blog/${postId}`);
  if (!result.ok) return problemState(result.problem);
  refreshPublicPages(slug);
  revalidatePath('/admin/blog');
  redirect('/admin/blog');
}
