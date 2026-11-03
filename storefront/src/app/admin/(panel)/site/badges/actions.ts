'use server';

import type { FormState } from '@/lib/admin/form-state';
import { runSiteIntent } from '@/lib/admin/site-actions-core';
import { parseBadgesForm } from '@/lib/admin/site-forms';

export async function badgesAction(_previous: FormState, formData: FormData): Promise<FormState> {
  return runSiteIntent('badges', parseBadgesForm, formData);
}
