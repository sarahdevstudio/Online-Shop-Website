'use server';

import { cookies } from 'next/headers';
import { redirect } from 'next/navigation';
import { COMPARE_COOKIE, COMPARE_MAX_AGE_SECONDS, parseCompare, serializeCompare, toggleCompare } from '@/lib/compare';

/** Only an address on this site is a valid place to go back to (no open redirect through a forged form). */
function safeReturn(value: FormDataEntryValue | null, fallback: string): string {
  return typeof value === 'string' && /^\/(?!\/)[^\s\\]*$/.test(value) ? value : fallback;
}

async function save(slugs: string[]) {
  const jar = await cookies();
  if (slugs.length === 0) jar.delete(COMPARE_COOKIE);
  else jar.set(COMPARE_COOKIE, serializeCompare(slugs), { httpOnly: true, secure: process.env.NODE_ENV === 'production', sameSite: 'lax', path: '/', maxAge: COMPARE_MAX_AGE_SECONDS });
}

/** Product page / compare page button: add the product, or take it out when it is already in the list. */
export async function toggleCompareAction(formData: FormData): Promise<void> {
  const slug = String(formData.get('slug') ?? '');
  const current = parseCompare((await cookies()).get(COMPARE_COOKIE)?.value);
  const result = toggleCompare(current, slug);
  if (/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(slug) && result.changed !== 'full') await save(result.slugs);
  redirect(safeReturn(formData.get('returnTo'), '/compare'));
}

export async function clearCompareAction(): Promise<void> {
  await save([]);
  redirect('/compare');
}
