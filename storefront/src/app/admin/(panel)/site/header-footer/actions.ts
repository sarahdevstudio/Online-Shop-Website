'use server';

import type { FormState } from '@/lib/admin/form-state';
import { runSiteIntent } from '@/lib/admin/site-actions-core';
import { parseChromePayload } from '@/lib/admin/site-chrome-model';
import type { ChromePayload } from '@/lib/admin/site-types';

export async function chromeAction(_previous: FormState, formData: FormData): Promise<FormState> {
  return runSiteIntent('chrome', (data) => (parseChromePayload(String(data.get('payload') ?? '')) ?? {}) as ChromePayload, formData);
}
