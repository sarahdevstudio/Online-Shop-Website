'use server';

import { revalidatePath } from 'next/cache';
import { redirect } from 'next/navigation';
import { adminFetch } from '@/lib/admin/api';
import { failState, okState, problemState, submittedFrom, type FormState } from '@/lib/admin/form-state';
import { checked, text } from '@/lib/admin/form-input';
import { adminMessages } from '@/lib/admin/messages';
import { staffMessages } from '@/lib/admin/staff-messages';
import type { StaffRole, StaffUser } from '@/lib/admin/staff-types';

/**
 * Staff and roles. Creating an account, changing roles or permissions and resetting a password send the caller's own password
 * as `currentPassword` (the API's step-up); a wrong one is shown next to that box. The password fields are never echoed back.
 */

const uuid = (formData: FormData, name = 'id') => {
  const value = text(formData, name);
  return /^[0-9a-f-]{36}$/i.test(value) ? value : null;
};

const secret = (formData: FormData, name: string) => String(formData.get(name) ?? '');

/** `submittedFrom` keeps one value per field name; the checkbox lists (roles, permissions) are kept whole, comma-separated, so a refused form can show them again. */
const submittedWith = (formData: FormData, ...lists: string[]): Record<string, string> => {
  const submitted = submittedFrom(formData);
  for (const name of lists) submitted[name] = formData.getAll(name).map(String).join(',');
  return submitted;
};

function withStepUp(state: FormState, code: string | null): FormState {
  return code === 'step_up_failed' ? { ...state, errors: { ...state.errors, currentPassword: adminMessages.stepUpFailed } } : state;
}

export async function createStaffAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const submitted = submittedWith(formData, 'roles');
  const body = {
    username: text(formData, 'username'),
    displayName: text(formData, 'displayName') || null,
    email: text(formData, 'email') || null,
    password: secret(formData, 'password'),
    roles: formData.getAll('roles').map(String),
    currentPassword: secret(formData, 'currentPassword') || null,
  };
  const result = await adminFetch<StaffUser>('POST', '/admin/staff', { body });
  if (!result.ok) return withStepUp(problemState(result.problem, submitted), result.problem.code);
  revalidatePath('/admin/staff');
  redirect(`/admin/staff/${result.body.id}`);
}

export async function updateStaffAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const id = uuid(formData);
  if (!id) return failState(adminMessages.failed);
  const result = await adminFetch<StaffUser>('PUT', `/admin/staff/${id}`, {
    body: { displayName: text(formData, 'displayName') || null, email: text(formData, 'email') || null, stamp: text(formData, 'stamp') },
  });
  if (!result.ok) return problemState(result.problem, submittedFrom(formData));
  revalidatePath(`/admin/staff/${id}`);
  return okState(adminMessages.saved);
}

export async function setStaffRolesAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const id = uuid(formData);
  if (!id) return failState(adminMessages.failed);
  const submitted = submittedWith(formData, 'roles');
  const result = await adminFetch<StaffUser>('PUT', `/admin/staff/${id}/roles`, {
    body: { roles: formData.getAll('roles').map(String), stamp: text(formData, 'stamp'), currentPassword: secret(formData, 'currentPassword') || null },
  });
  if (!result.ok) return withStepUp(problemState(result.problem, submitted), result.problem.code);
  revalidatePath(`/admin/staff/${id}`);
  revalidatePath('/admin/staff');
  return okState(staffMessages.rolesSaved);
}

export async function setStaffActiveAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const id = uuid(formData);
  if (!id) return failState(adminMessages.failed);
  const active = checked(formData, 'activate');
  const result = await adminFetch<StaffUser>('PUT', `/admin/staff/${id}/active`, { body: { isActive: active, stamp: text(formData, 'stamp') } });
  if (!result.ok) return problemState(result.problem, submittedFrom(formData));
  revalidatePath(`/admin/staff/${id}`);
  revalidatePath('/admin/staff');
  return okState(active ? staffMessages.activated : staffMessages.deactivated);
}

export async function resetStaffPasswordAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const id = uuid(formData);
  if (!id) return failState(adminMessages.failed);
  const result = await adminFetch<StaffUser>('PUT', `/admin/staff/${id}/password`, {
    body: { newPassword: secret(formData, 'newPassword'), stamp: text(formData, 'stamp'), currentPassword: secret(formData, 'currentPassword') || null },
  });
  if (!result.ok) return withStepUp(problemState(result.problem, submittedFrom(formData)), result.problem.code);
  revalidatePath(`/admin/staff/${id}`);
  return okState(staffMessages.resetDone);
}

export async function saveRoleAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const id = uuid(formData);
  const submitted = submittedWith(formData, 'permissions');
  const body = {
    name: text(formData, 'name'),
    title: text(formData, 'title') || null,
    permissions: formData.getAll('permissions').map(String),
    stamp: id ? text(formData, 'stamp') : null,
    currentPassword: secret(formData, 'currentPassword') || null,
  };
  const result = await adminFetch<StaffRole>(id ? 'PUT' : 'POST', id ? `/admin/staff/roles/${id}` : '/admin/staff/roles', { body });
  if (!result.ok) return withStepUp(problemState(result.problem, submitted), result.problem.code);
  revalidatePath('/admin/staff/roles');
  if (!id) redirect(`/admin/staff/roles/${result.body.id}`);
  revalidatePath(`/admin/staff/roles/${id}`);
  return okState(staffMessages.roleSaved);
}

export async function deleteRoleAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const id = uuid(formData);
  if (!id) return failState(adminMessages.failed);
  const result = await adminFetch<unknown>('POST', `/admin/staff/roles/${id}/delete`, {
    body: { stamp: text(formData, 'stamp'), currentPassword: secret(formData, 'currentPassword') || null },
  });
  if (!result.ok) return withStepUp(problemState(result.problem, submittedFrom(formData)), result.problem.code);
  revalidatePath('/admin/staff/roles');
  redirect('/admin/staff/roles');
}
