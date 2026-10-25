import { parseLocalizedInteger } from './format';

/** Form values arrive as strings (possibly in Persian digits); anything unusable becomes null / a safe default. */

export function parseVariantId(value: FormDataEntryValue | null): number | null {
  const n = typeof value === 'string' ? parseLocalizedInteger(value) : null;
  return n !== null && Number.isSafeInteger(n) && n > 0 ? n : null;
}

/** Quantity 1..1000 (the API clamps to stock and the configured maximum); `allowZero` is for "update", where 0 removes. */
export function parseQuantity(value: FormDataEntryValue | null, allowZero = false): number | null {
  const n = typeof value === 'string' ? parseLocalizedInteger(value) : null;
  if (n === null || n > 1000) return null;
  return n >= (allowZero ? 0 : 1) ? n : null;
}

/** Maps an API problem to a key of `messages.cartErrors`. */
export function cartErrorKey(status: number, code: string | null): string {
  if (status === 429) return 'rate_limited';
  return code ?? 'invalid';
}
