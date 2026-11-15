'use client';

import { useActionState, useRef } from 'react';
import { CheckField, Form, FormActions, FormGrid, FormSection, RequiredNote, SubmitButton, TextField } from '@/components/form';
import { INITIAL_FORM_STATE } from '@/lib/admin/form-state';
import { staffMessages } from '@/lib/admin/staff-messages';
import type { StaffRole } from '@/lib/admin/staff-types';
import { FormBanner } from '../../../_components/FormBits';
import { deleteRoleAction, saveRoleAction } from '../actions';
import { StepUpSection } from '../StaffForms';
import styles from '../../../admin.module.css';

const t = staffMessages;

/** `permissions` ("Resource.Action" codes, e.g. "Catalog.View") grouped by resource, in the server's own order. */
function byResource(permissions: string[]): { resource: string; codes: string[] }[] {
  const rows: { resource: string; codes: string[] }[] = [];
  for (const code of permissions) {
    const resource = code.split('.')[0];
    const row = rows.find((r) => r.resource === resource);
    if (row) row.codes.push(code);
    else rows.push({ resource, codes: [code] });
  }

  return rows;
}

/** One matrix row: the resource's label, a checkbox per action it supports, and a "select all" for the row. */
function PermissionRow({ resource, codes, granted, error }: { resource: string; codes: string[]; granted: string[]; error?: string }) {
  const rowRef = useRef<HTMLTableRowElement>(null);
  const toggleAll = (checked: boolean) => {
    rowRef.current?.querySelectorAll<HTMLInputElement>('input[type="checkbox"][name="permissions"]').forEach((box) => {
      box.checked = checked;
    });
  };

  return (
    <tr ref={rowRef}>
      <th scope="row">{t.resourceLabel(resource)}</th>
      {codes.length > 1 && (
        <td>
          <label className="ui-check-row">
            <input type="checkbox" className="ui-check" defaultChecked={codes.every((c) => granted.includes(c))} onChange={(e) => toggleAll(e.target.checked)} aria-label={t.selectAllRow} />
          </label>
        </td>
      )}
      {codes.length === 1 && <td />}
      {codes.map((code) => (
        <td key={code}>
          <CheckField name="permissions" value={code} label={t.permissionActionLabel(code.split('.')[1])} defaultChecked={granted.includes(code)} error={error} />
        </td>
      ))}
    </tr>
  );
}

/** Create (no `role`) or edit a custom role: its name, title and the permissions it grants, as a resource × action matrix. */
export function RoleForm({ role, permissions }: { role?: StaffRole; permissions: string[] }) {
  const [state, action] = useActionState(saveRoleAction, INITIAL_FORM_STATE);
  const typed = Object.keys(state.submitted).length > 0;
  const granted = typed ? (state.submitted.permissions ?? '').split(',').filter(Boolean) : (role?.permissions ?? []);
  const rows = byResource(permissions);
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
          <div className={styles.tableWrap} tabIndex={0} role="region" aria-label={t.rolePermissions}>
            <table className={styles.table}>
              <tbody>
                {rows.map((row) => (
                  <PermissionRow key={row.resource} resource={row.resource} codes={row.codes} granted={granted} error={state.errors.permissions} />
                ))}
              </tbody>
            </table>
          </div>
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
