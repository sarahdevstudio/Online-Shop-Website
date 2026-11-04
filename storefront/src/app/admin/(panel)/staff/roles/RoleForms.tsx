'use client';

import { useActionState } from 'react';
import { CheckField, Form, FormActions, FormGrid, FormSection, RequiredNote, SubmitButton, TextField } from '@/components/form';
import { INITIAL_FORM_STATE } from '@/lib/admin/form-state';
import { staffMessages } from '@/lib/admin/staff-messages';
import type { StaffRole } from '@/lib/admin/staff-types';
import { FormBanner } from '../../../_components/FormBits';
import { deleteRoleAction, saveRoleAction } from '../actions';
import { StepUpSection } from '../StaffForms';

const t = staffMessages;

/** Create (no `role`) or edit a custom role: its name, title and the permissions it grants. */
export function RoleForm({ role, permissions }: { role?: StaffRole; permissions: string[] }) {
  const [state, action] = useActionState(saveRoleAction, INITIAL_FORM_STATE);
  const typed = Object.keys(state.submitted).length > 0;
  const granted = typed ? (state.submitted.permissions ?? '').split(',').filter(Boolean) : (role?.permissions ?? []);
  return (
    <Form action={action} errors={state.errors} banner={<FormBanner state={state} />}>
      {role && <input type="hidden" name="id" value={role.id} />}
      {role && <input type="hidden" name="stamp" value={role.stamp} />}
      <RequiredNote />
      <div key={role?.stamp ?? 'new'} style={{ display: 'contents' }}>
        <FormSection card title={role ? undefined : t.newRole}>
          <FormGrid>
            <TextField name="name" label={t.roleName} hint={t.roleNameHint} ltr autoComplete="off" maxLength={40} defaultValue={typed ? state.submitted.name : role?.name} required />
            <TextField name="title" label={t.roleTitle} autoComplete="off" maxLength={80} defaultValue={typed ? state.submitted.title : role?.title} required />
          </FormGrid>
        </FormSection>
        <FormSection card title={t.rolePermissions} hint={`${t.rolePermissionsHint} ${t.lockoutNote}`}>
          <FormGrid>
            {permissions.map((code) => (
              <CheckField key={code} name="permissions" value={code} label={t.permissionLabel(code)} defaultChecked={granted.includes(code)} error={state.errors.permissions} />
            ))}
          </FormGrid>
        </FormSection>
      </div>
      <FormSection card>
        <StepUpSection error={state.errors.currentPassword} />
      </FormSection>
      <FormActions>
        <SubmitButton>{role ? t.saveRole : t.createRole}</SubmitButton>
      </FormActions>
    </Form>
  );
}

export function DeleteRoleForm({ role }: { role: StaffRole }) {
  const [state, action] = useActionState(deleteRoleAction, INITIAL_FORM_STATE);
  return (
    <details>
      <summary>{t.deleteRole}</summary>
      <Form action={action} errors={state.errors} banner={<FormBanner state={state} />}>
        <input type="hidden" name="id" value={role.id} />
        <input type="hidden" name="stamp" value={role.stamp} />
        <FormSection hint={t.deleteRoleHint}>
          <StepUpSection error={state.errors.currentPassword} />
          <FormActions>
            <SubmitButton variant="danger">{t.deleteRoleConfirm}</SubmitButton>
          </FormActions>
        </FormSection>
      </Form>
    </details>
  );
}
