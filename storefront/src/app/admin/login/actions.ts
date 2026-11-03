'use server';

import { redirect } from 'next/navigation';
import { writeSessionCookie } from '@/lib/admin/api';
import { LoginLimiter, loginLimiter } from '@/lib/admin/login-limiter';
import { apiAdminLogin } from '@/lib/admin/login';
import { adminMessages } from '@/lib/admin/messages';
import { clientIpForApi } from '@/lib/store-api';

export interface LoginState {
  /** The single generic failure text (bad credentials, locked, throttled): reveals nothing. */
  error: string | null;
  /** field → message, only for a field left empty. */
  errors: Record<string, string>;
}

// Per IP (any username) as well as per IP + username; the API adds its own per-IP and per-account limits and the lockout.
const ipLimiter = new LoginLimiter(15, 60_000);

export async function loginAction(_previous: LoginState, formData: FormData): Promise<LoginState> {
  const username = String(formData.get('username') ?? '').trim();
  const password = String(formData.get('password') ?? '');
  const failed: LoginState = { error: adminMessages.loginFailed, errors: {} };
  const missing: Record<string, string> = {};
  if (!username) missing.username = adminMessages.usernameRequired;
  if (!password) missing.password = adminMessages.passwordRequired;
  if (Object.keys(missing).length > 0) return { error: null, errors: missing };
  if (username.length > 256 || password.length > 128) return failed;

  const ip = (await clientIpForApi()) ?? 'unknown';
  if (!ipLimiter.tryAcquire(ip) || !loginLimiter.tryAcquire(`${ip}|${username.toLowerCase()}`)) return failed;

  const account = await apiAdminLogin(username, password);
  if (!account) return failed;

  const now = Date.now();
  await writeSessionCookie({ v: 1, auth: account.auth, username: account.username, iat: now, seen: now });
  redirect('/admin');
}
