import { apiBaseUrl } from '../api';
import { currentTexts, defaultText, setTexts, type TextKey } from './texts';

/**
 * Server side of the dictionary: loads the edited texts from the API, keeps them in memory for `TTL_MS`, and falls back to the
 * previous (or the shipped) texts when the API cannot be reached - the pages never wait long for it and never fail because of it.
 * Other instances see an edit within the TTL; the instance that served the edit refreshes at once (`refreshTexts`).
 */

const TTL_MS = 30_000;
const RETRY_MS = 10_000;
const TIMEOUT_MS = 3_000;

interface Loaded {
  at: number;
  ok: boolean;
}

const state: Record<'shop' | 'admin', Loaded> = { shop: { at: 0, ok: false }, admin: { at: 0, ok: false } };
const maps: Record<'shop' | 'admin', Record<string, string>> = { shop: {}, admin: {} };
const inflight: Partial<Record<'shop' | 'admin', Promise<void>>> = {};

function publish() {
  setTexts({ ...maps.shop, ...maps.admin });
}

/** Only entries that differ from the shipped text are kept: that is all a client needs, and it keeps the payload tiny. */
function edits(received: Record<string, string>): Record<string, string> {
  const result: Record<string, string> = {};
  for (const [key, text] of Object.entries(received)) {
    if (typeof text === 'string' && text.length > 0 && text !== defaultText(key as TextKey)) result[key] = text;
  }
  return result;
}

async function fetchShop(): Promise<Record<string, string>> {
  const response = await fetch(`${apiBaseUrl()}/api/v1/catalog/translations`, { headers: { accept: 'application/json' }, cache: 'no-store', signal: AbortSignal.timeout(TIMEOUT_MS) });
  if (!response.ok) throw new Error(`translations returned ${response.status}`);
  return ((await response.json()) as { result: Record<string, string> | null }).result ?? {};
}

async function load(scope: 'shop' | 'admin', fetcher: () => Promise<Record<string, string>>) {
  try {
    maps[scope] = edits(await fetcher());
    state[scope] = { at: Date.now(), ok: true };
    publish();
  } catch {
    // Keep whatever is loaded (or the shipped texts) and try again soon; a log line per outage would only add noise to the page path.
    state[scope] = { at: Date.now(), ok: false };
  }
}

async function ensure(scope: 'shop' | 'admin', fetcher: () => Promise<Record<string, string>>, force: boolean) {
  const { at, ok } = state[scope];
  const fresh = Date.now() - at < (ok ? TTL_MS : RETRY_MS);
  if (!force && fresh) return;
  inflight[scope] ??= load(scope, fetcher).finally(() => {
    inflight[scope] = undefined;
  });
  await inflight[scope];
}

/** Makes sure the storefront texts are loaded (cheap when fresh). */
export function ensureTexts(force = false) {
  return ensure('shop', fetchShop, force);
}

/** Same for the admin panel's texts; `fetcher` is the admin API call (it carries the admin session). */
export function ensureAdminTexts(fetcher: () => Promise<Record<string, string>>, force = false) {
  return ensure('admin', fetcher, force);
}

/** The texts that differ from the shipped ones, for `<TextsProvider>`. */
export function clientTexts(prefixes: readonly string[]): Record<string, string> {
  const all = currentTexts();
  return Object.fromEntries(Object.entries(all).filter(([key]) => prefixes.some((p) => key.startsWith(p))));
}
