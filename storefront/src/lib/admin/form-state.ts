import type { AdminProblem } from './api';
import { adminMessages } from './messages';

/** What an admin Server Action returns to its `useActionState` form. */
export interface FormState {
  ok: boolean;
  message: string | null;
  /** field → first message, shown next to the field. */
  errors: Record<string, string>;
  /** The record was changed by someone else (HTTP 409 "data changed"): the form offers Reload. */
  conflict: boolean;
  /** What the admin had typed, shown after a conflict so it can be re-applied by hand. */
  submitted: Record<string, string>;
}

export const INITIAL_FORM_STATE: FormState = { ok: false, message: null, errors: {}, conflict: false, submitted: {} };

export const okState = (message: string): FormState => ({ ...INITIAL_FORM_STATE, ok: true, message });

export const failState = (message: string, submitted: Record<string, string> = {}, errors: Record<string, string> = {}): FormState => ({
  ...INITIAL_FORM_STATE,
  message,
  errors,
  submitted,
});

/** `FullName` / `variants[0].Sku` → `fullName` / `variants[0].sku`. */
export function camelKeys(errors: Record<string, string[]> | null): Record<string, string> {
  const result: Record<string, string> = {};
  for (const [key, list] of Object.entries(errors ?? {})) {
    if (list.length === 0) continue;
    const camel = key.replace(/(^|\.)([A-Z])/g, (_m, dot: string, letter: string) => dot + letter.toLowerCase());
    result[camel] = list[0];
  }
  return result;
}

/** A 409 without a business code is the database's concurrency check (row version): "reload". */
export function isConflict(problem: AdminProblem): boolean {
  return problem.status === 409 && (problem.code === null || problem.code === 'concurrent_update');
}

/** One place that turns an API problem into form state: conflict banner, field errors, or a message. */
export function problemState(problem: AdminProblem, submitted: Record<string, string> = {}): FormState {
  if (isConflict(problem)) {
    return { ...INITIAL_FORM_STATE, conflict: true, message: adminMessages.conflict, submitted };
  }
  const errors = camelKeys(problem.errors);
  if (problem.status === 400 && Object.keys(errors).length > 0) return failState(adminMessages.fixErrors, submitted, errors);
  if (problem.status === 403) return failState(problem.code === 'step_up_failed' ? adminMessages.stepUpFailed : adminMessages.forbidden, submitted);
  if (problem.status === 404) return failState(adminMessages.notFound, submitted);
  if (problem.status === 429) return failState(adminMessages.rateLimited, submitted);
  return failState(problem.detail ?? adminMessages.failed, submitted);
}

/** Text fields of a form as a plain record (never files), for `submitted`. */
export function submittedFrom(formData: FormData, skip: readonly string[] = ['currentPassword', 'newPassword', 'password']): Record<string, string> {
  const result: Record<string, string> = {};
  for (const [key, value] of formData.entries()) {
    if (typeof value === 'string' && !skip.includes(key) && !key.startsWith('$ACTION')) result[key] = value;
  }
  return result;
}
