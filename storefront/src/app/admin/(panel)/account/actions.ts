'use server';

import { adminFetch, cookieValue, readAdminSession, writeSessionCookie } from '@/lib/admin/api';
import { failState, okState, problemState, type FormState } from '@/lib/admin/form-state';
import { adminMessages } from '@/lib/admin/messages';

/**
 * Changes the admin's own password. The API closes every other session of this user and answers with a fresh auth cookie
 * for this one, which replaces the value inside the sealed `admin_session`.
 */
export async function changePasswordAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const currentPassword = String(formData.get('currentPassword') ?? '');
  const newPassword = String(formData.get('newPassword') ?? '');
  if (!currentPassword || !newPassword) return failState(adminMessages.fixErrors);

  const session = await readAdminSession();
  const result = await adminFetch<unknown>('POST', '/auth/change-password', { body: { currentPassword, newPassword } });
  if (!result.ok) return problemState(result.problem);

  const renewed = cookieValue(result.setCookies, 'shop_auth');
  if (session && renewed) await writeSessionCookie({ ...session, auth: renewed, seen: Date.now() });
  return okState(adminMessages.passwordChanged);
}
