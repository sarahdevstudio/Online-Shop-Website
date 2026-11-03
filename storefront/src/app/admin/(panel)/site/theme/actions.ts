'use server';

import type { FormState } from '@/lib/admin/form-state';
import { runSiteIntent } from '@/lib/admin/site-actions-core';
import { parseThemeForm } from '@/lib/admin/site-forms';

export async function themeAction(_previous: FormState, formData: FormData): Promise<FormState> {
  return runSiteIntent('theme', parseThemeForm, formData);
}
