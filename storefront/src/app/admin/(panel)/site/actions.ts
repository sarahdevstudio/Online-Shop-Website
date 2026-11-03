'use server';

import type { FormState } from '@/lib/admin/form-state';
import { runSiteRestore } from '@/lib/admin/site-actions-core';

/** Restore a version into the draft (`intent=restore`) or restore and publish it (`intent=restorePublish`). */
export async function restoreSiteAction(_previous: FormState, formData: FormData): Promise<FormState> {
  return runSiteRestore(formData);
}
