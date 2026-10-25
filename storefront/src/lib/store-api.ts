import { headers } from 'next/headers';
import { ApiUnavailableError, apiBaseUrl } from './api';
import { resolveClientIp, trustedProxyHops } from './client-ip';
import { maskTokens } from './log';
import type { Problem } from './store-types';

/**
 * Server-side client for `/api/v1/store/*`. Called only from Server Components and Server Actions: the browser never
 * talks to the API, and the shared key never leaves the server. Responses are never cached.
 */

export type StoreResult<T> = { ok: true; status: number; body: T } | { ok: false; status: number; problem: Problem };

interface StoreRequest {
  body?: unknown;
  cartToken?: string;
  idempotencyKey?: string;
}

const TIMEOUT_MS = 10_000;
const DEV_KEY = 'dev-storefront-key-not-a-secret-0123456789';

function storefrontKey(): string {
  const key = process.env.STOREFRONT_API_KEY;
  if (key) return key;
  if (process.env.NODE_ENV === 'development') return DEV_KEY;
  throw new ApiUnavailableError('STOREFRONT_API_KEY is not configured.');
}

/** The visitor's address from the trusted proxy only (see resolveClientIp); null when it can't be determined. */
async function clientIp(): Promise<string | null> {
  const hops = trustedProxyHops();
  const development = process.env.NODE_ENV === 'development';
  if (hops === 0 && process.env.NODE_ENV === 'production') {
    // Without it every visitor would share the storefront's address and its rate limits.
    throw new ApiUnavailableError('TRUSTED_PROXY_HOPS must be set (number of reverse proxies in front of Next.js).');
  }
  return resolveClientIp((await headers()).get('x-forwarded-for'), hops, development);
}

export async function storeFetch<T>(method: string, path: string, request: StoreRequest = {}): Promise<StoreResult<T>> {
  const label = `${method} ${maskTokens(path)}`; // never put a raw token into a message
  const requestHeaders: Record<string, string> = { accept: 'application/json', 'x-storefront-key': storefrontKey() };
  if (request.cartToken) requestHeaders['x-cart-token'] = request.cartToken;
  if (request.idempotencyKey) requestHeaders['idempotency-key'] = request.idempotencyKey;
  const ip = await clientIp();
  if (ip) requestHeaders['x-forwarded-for'] = ip; // one address, always overwritten: a browser value is never passed on
  if (request.body !== undefined) requestHeaders['content-type'] = 'application/json';

  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl()}/api/v1/store${path}`, {
      method,
      headers: requestHeaders,
      body: request.body === undefined ? undefined : JSON.stringify(request.body),
      cache: 'no-store',
      signal: AbortSignal.timeout(TIMEOUT_MS),
    });
  } catch (error) {
    throw new ApiUnavailableError(`${label} failed`, { cause: error });
  }

  if (response.status === 401) throw new Error(`${label} was rejected: STOREFRONT_API_KEY does not match the API's Storefront:ApiKey.`);
  if (response.status >= 500) throw new ApiUnavailableError(`${label} returned ${response.status}`);

  const json: unknown = await response.json().catch(() => null);
  if (response.ok) return { ok: true, status: response.status, body: json as T };

  const raw = (json ?? {}) as Record<string, unknown>;
  return {
    ok: false,
    status: response.status,
    problem: {
      status: response.status,
      code: typeof raw.code === 'string' ? raw.code : null,
      detail: typeof raw.detail === 'string' ? raw.detail : null,
      errors: (raw.errors as Record<string, string[]> | undefined) ?? null,
      lines: (raw.lines as Problem['lines']) ?? null,
    },
  };
}
