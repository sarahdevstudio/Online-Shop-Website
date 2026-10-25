import { isIP } from 'node:net';

/**
 * The visitor's address as the API should see it, derived ONLY from what a trusted reverse proxy appended.
 *
 * `X-Forwarded-For` is `<whatever the browser sent>, <addresses appended by proxies>`. With `trustedHops` proxies in
 * front of Next (each appending the address of its own peer), the real client is the entry `trustedHops` places from the
 * right; anything further left was supplied by the browser and is ignored.
 *
 * With no trusted proxy (`trustedHops` 0) Next keeps a browser-supplied header unchanged and only fills it with the
 * socket address when the browser sent none, so the header can't be told apart from a spoof. It is therefore trusted in
 * local development only (one value, the developer's own socket address); otherwise null is returned and the API falls
 * back to the storefront's own address.
 */
export function resolveClientIp(forwardedFor: string | null | undefined, trustedHops: number, development: boolean): string | null {
  const chain = (forwardedFor ?? '')
    .split(',')
    .map((part) => part.trim())
    .filter(Boolean);
  if (chain.length === 0) return null;

  if (trustedHops <= 0) {
    return development && chain.length === 1 && isIP(chain[0]) !== 0 ? chain[0] : null;
  }
  if (chain.length < trustedHops) return null;

  const candidate = chain[chain.length - trustedHops];
  return isIP(candidate) !== 0 ? candidate : null;
}

/** `TRUSTED_PROXY_HOPS`: how many reverse proxies sit in front of Next (0 = none). Invalid values count as 0. */
export function trustedProxyHops(value: string | undefined = process.env.TRUSTED_PROXY_HOPS): number {
  const hops = Number.parseInt(value ?? '', 10);
  return Number.isInteger(hops) && hops > 0 && hops <= 5 ? hops : 0;
}
