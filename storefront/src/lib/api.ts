import { connection } from 'next/server';

/**
 * Server-side access to the catalog API. The base URL is server-only (API_BASE_URL, never NEXT_PUBLIC_*), so the
 * API can stay on a private network and is never called from the browser.
 */

/** The API could not be reached, timed out or answered 5xx. Pages let it propagate to the error page (HTTP 500). */
export class ApiUnavailableError extends Error {
  constructor(message: string, options?: { cause?: unknown }) {
    super(message, options);
    this.name = 'ApiUnavailableError';
  }
}

/** Seconds a successful response stays in Next's data cache. Only 200 responses are ever stored. */
export const revalidate = {
  products: 60,
  categories: 300,
  sitemap: 300,
} as const;

const TIMEOUT_MS = 10_000;

export function apiBaseUrl(): string {
  const configured = process.env.API_BASE_URL;
  if (configured) return configured.replace(/\/+$/, '');
  if (process.env.NODE_ENV === 'development') return 'http://localhost:5167';
  throw new ApiUnavailableError('API_BASE_URL is not configured.');
}

/**
 * GET `/api/v1{path}`. Returns null only for a real API 404; network errors, timeouts and 5xx throw
 * ApiUnavailableError; any other status is a bug in this client and throws a plain Error.
 */
export async function apiGet<T>(path: string, seconds: number): Promise<T | null> {
  // Request-time only: `next build` must never need the API, so nothing that reads it is prerendered.
  await connection();

  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl()}/api/v1${path}`, {
      headers: { accept: 'application/json' },
      next: { revalidate: seconds },
      signal: AbortSignal.timeout(TIMEOUT_MS),
    });
  } catch (error) {
    throw new ApiUnavailableError(`GET ${path} failed`, { cause: error });
  }

  if (response.status === 404) return null;
  if (response.status >= 500) throw new ApiUnavailableError(`GET ${path} returned ${response.status}`);
  if (!response.ok) throw new Error(`GET ${path} returned ${response.status}`);
  return (await response.json()) as T;
}
