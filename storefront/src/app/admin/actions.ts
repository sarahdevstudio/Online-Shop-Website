'use server';

import { redirect } from 'next/navigation';
import { adminFetch, clearSessionCookie, readAdminSession } from '@/lib/admin/api';

/**
 * Signs out: the API revokes EVERY session of this user (security stamp), then the sealed cookie is deleted. A copied
 * cookie is useless from this moment on, on any device.
 */
export async function logoutAction(): Promise<void> {
  if (await readAdminSession()) {
    await adminFetch<unknown>('POST', '/auth/logout');
  }
  await clearSessionCookie();
  redirect('/admin/login');
}
