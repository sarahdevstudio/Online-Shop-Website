'use server';

import type { FormState } from '@/lib/admin/form-state';
import { runSiteIntent } from '@/lib/admin/site-actions-core';
import { parsePayloadField } from '@/lib/admin/site-home-model';

export async function homeAction(_previous: FormState, formData: FormData): Promise<FormState> {
  return runSiteIntent('home', (data) => parsePayloadField(data.get('payload')), formData);
}
