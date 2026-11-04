'use server';

import { revalidatePath } from 'next/cache';
import { redirect } from 'next/navigation';
import { adminFetch } from '@/lib/admin/api';
import { id as readId, text } from '@/lib/admin/form-input';
import { failState, problemState, type FormState } from '@/lib/admin/form-state';
import { adminMessages } from '@/lib/admin/messages';
import { refreshPublicPaths } from '@/lib/admin/route-actions';

/** Only ever go back to this page (the hidden field carries its own filter and page). */
function backTarget(formData: FormData): string {
  const back = text(formData, 'back');
  return /^\/admin\/redirects(\?[A-Za-z0-9=&%_.-]*)?$/.test(back) ? back : '/admin/redirects';
}

/**
 * Releases (deletes) an old path or the path of a deleted entity: it stops redirecting and can be taken by another entity.
 * Needs Routes.Manage (the API checks it) and is audited there.
 */
export async function releaseRedirectAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const routeId = readId(formData);
  if (routeId === null) return failState(adminMessages.failed);
  const result = await adminFetch<unknown>('DELETE', `/admin/routes/${routeId}`);
  if (!result.ok) return problemState(result.problem);

  refreshPublicPaths();
  revalidatePath('/admin/redirects');
  const back = backTarget(formData);
  redirect(`${back}${back.includes('?') ? '&' : '?'}released=1`);
}
