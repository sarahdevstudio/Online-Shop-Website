import { ApiUnavailableError, apiBaseUrl } from '../api';
import { clientIpForApi } from '../store-api';
import { cookieValue, fetchCsrf } from './api';

const TIMEOUT_MS = 15_000;

/**
 * Signs in against the API on the server. Returns the API's auth cookie value, or null for EVERY kind of failure (wrong
 * password, unknown e-mail, locked account, throttled, correct password but not an admin), so the caller can show one text.
 * An API that is down throws (that is not a credentials problem).
 */
export async function apiAdminLogin(email: string, password: string): Promise<{ auth: string; email: string } | null> {
  const ip = await clientIpForApi();
  const forwarded: Record<string, string> = ip ? { 'x-forwarded-for': ip } : {};

  const csrf = await fetchCsrf();
  let login: Response;
  try {
    login = await fetch(`${apiBaseUrl()}/api/v1/auth/login`, {
      method: 'POST',
      headers: {
        ...forwarded,
        accept: 'application/json',
        'content-type': 'application/json',
        cookie: `shop_csrf=${csrf.cookie}`,
        [csrf.headerName.toLowerCase()]: csrf.token,
      },
      body: JSON.stringify({ email, password }),
      cache: 'no-store',
      signal: AbortSignal.timeout(TIMEOUT_MS),
    });
  } catch (error) {
    throw new ApiUnavailableError('POST /auth/login failed', { cause: error });
  }
  if (login.status >= 500) throw new ApiUnavailableError(`POST /auth/login returned ${login.status}`);
  if (!login.ok) return null;

  const auth = cookieValue(login.headers.getSetCookie(), 'shop_auth');
  if (!auth) return null;

  const account = (await login.json().catch(() => null)) as { email?: string; roles?: string[] } | null;
  if (!account?.roles?.includes('Admin')) {
    // A customer account: not allowed here. End that session again (this also revokes its other sessions).
    await endSession(auth, forwarded);
    return null;
  }
  return { auth, email: account.email ?? email };
}

async function endSession(auth: string, forwarded: Record<string, string>) {
  try {
    const csrf = await fetchCsrf(auth);
    await fetch(`${apiBaseUrl()}/api/v1/auth/logout`, {
      method: 'POST',
      headers: { ...forwarded, cookie: `shop_auth=${auth}; shop_csrf=${csrf.cookie}`, [csrf.headerName.toLowerCase()]: csrf.token },
      cache: 'no-store',
      signal: AbortSignal.timeout(TIMEOUT_MS),
    });
  } catch {
    // best effort; the account is refused either way
  }
}
