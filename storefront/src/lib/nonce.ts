import { headers } from 'next/headers';

/** The per-request CSP nonce set by the proxy (undefined when the proxy did not run, e.g. in a unit test). */
export async function requestNonce(): Promise<string | undefined> {
  return (await headers()).get('x-nonce') ?? undefined;
}
