'use server';

import type { FormState } from '@/lib/admin/form-state';
import { runSiteIntent } from '@/lib/admin/site-actions-core';
import { parseSeoForm } from '@/lib/admin/site-forms';

export async function seoAction(_previous: FormState, formData: FormData): Promise<FormState> {
  return runSiteIntent('seo', parseSeoForm, formData);
}
