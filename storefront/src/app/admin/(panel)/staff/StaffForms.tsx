'use client';

import { useActionState } from 'react';
import { CheckField, Form, FormActions, FormGrid, FormSection, RequiredNote, SubmitButton, TextField } from '@/components/form';
import { INITIAL_FORM_STATE } from '@/lib/admin/form-state';
import { adminMessages } from '@/lib/admin/messages';
import { staffMessages } from '@/lib/admin/staff-messages';
import { roleLabel, type StaffRole, type StaffUser } from '@/lib/admin/staff-types';
import { FormBanner } from '../../_components/FormBits';
import { createStaffAction, resetStaffPasswordAction, setStaffActiveAction, setStaffRolesAction, updateStaffAction } from './actions';

const t = staffMessages;
const roleNames = t.roleNames;

/** The caller's own password again: asked on every form that changes who may do what. */
export function StepUpSection({ error }: { error?: string }) {
  return (
    <FormSection title={t.stepUpTitle} hint={t.stepUpHint}>
      <FormGrid>
        <TextField name="currentPassword" label={adminMessages.currentPassword} type="password" ltr autoComplete="current-password" error={error} required />
      </FormGrid>
    </FormSection>
  );
}

/** The roles a staff account can hold (Customer is not one of them). */
function RolePicker({ roles, selected, error }: { roles: StaffRole[]; selected: string[]; error?: string }) {
  return (
    <FormGrid>
      {roles
        .filter((role) => role.name !== 'Customer')
        .map((role) => (
          <CheckField key={role.id} name="roles" value={role.name} label={roleLabel(role, roleNames())} defaultChecked={selected.includes(role.name)} error={error} />
        ))}
    </FormGrid>
  );
}

export function CreateStaffForm({ roles }: { roles: StaffRole[] }) {
  const [state, action] = useActionState(createStaffAction, INITIAL_FORM_STATE);
  const typed = Object.keys(state.submitted).length > 0;
  const v = (name: string) => (typed ? state.submitted[name] : undefined);
  return (
    <Form action={action} errors={state.errors} banner={<FormBanner state={state} />}>
      <RequiredNote />
      <FormSection card title={t.newUser}>
        <FormGrid>
          <TextField name="username" label={t.username} hint={t.usernameHint} ltr autoComplete="off" maxLength={64} defaultValue={v('username')} required />
          <TextField name="displayName" label={t.displayName} autoComplete="off" maxLength={128} defaultValue={v('displayName')} />
          <TextField name="email" label={t.email} type="email" ltr autoComplete="off" maxLength={256} defaultValue={v('email')} />
          <TextField name="password" label={t.password} hint={t.passwordHint} type="password" ltr autoComplete="new-password" maxLength={128} required />
        </FormGrid>
      </FormSection>
      <FormSection card title={t.rolesField} hint={t.rolesHint}>
        <RolePicker roles={roles} selected={typed ? (state.submitted.roles ?? '').split(',').filter(Boolean) : []} error={state.errors.roles} />
      </FormSection>
      <FormSection card>
        <StepUpSection error={state.errors.currentPassword} />
      </FormSection>
      <FormActions>
        <SubmitButton>{t.createUser}</SubmitButton>
      </FormActions>
    </Form>
  );
}

export function ProfileForm({ user }: { user: StaffUser }) {
  const [state, action] = useActionState(updateStaffAction, INITIAL_FORM_STATE);
  return (
    <Form action={action} errors={state.errors} banner={<FormBanner state={state} />}>
      <input type="hidden" name="id" value={user.id} />
      <input type="hidden" name="stamp" value={user.stamp} />
      <FormSection card title={t.profile}>
        <div key={user.stamp} style={{ display: 'contents' }}>
          <FormGrid>
            <TextField name="displayName" label={t.displayName} autoComplete="off" maxLength={128} defaultValue={user.displayName} />
            <TextField name="email" label={t.email} type="email" ltr autoComplete="off" maxLength={256} defaultValue={user.email} />
          </FormGrid>
        </div>
        <FormActions>
          <SubmitButton>{t.saveProfile}</SubmitButton>
        </FormActions>
      </FormSection>
    </Form>
  );
}

export function UserRolesForm({ user, roles }: { user: StaffUser; roles: StaffRole[] }) {
  const [state, action] = useActionState(setStaffRolesAction, INITIAL_FORM_STATE);
  return (
    <Form action={action} errors={state.errors} banner={<FormBanner state={state} />}>
      <input type="hidden" name="id" value={user.id} />
      <input type="hidden" name="stamp" value={user.stamp} />
      <FormSection card title={t.rolesTitle} hint={`${t.rolesChangedHint} ${t.lockoutNote}`}>
        <div key={user.stamp} style={{ display: 'contents' }}>
          <RolePicker roles={roles} selected={state.ok || Object.keys(state.submitted).length === 0 ? user.roles : (state.submitted.roles ?? '').split(',').filter(Boolean)} error={state.errors.roles} />
        </div>
        <StepUpSection error={state.errors.currentPassword} />
        <FormActions>
          <SubmitButton>{t.saveRoles}</SubmitButton>
        </FormActions>
      </FormSection>
    </Form>
  );
}

export function ActiveForm({ user, isSelf }: { user: StaffUser; isSelf: boolean }) {
  const [state, action] = useActionState(setStaffActiveAction, INITIAL_FORM_STATE);
  return (
    <Form action={action} errors={state.errors} banner={<FormBanner state={state} />}>
      <input type="hidden" name="id" value={user.id} />
      <input type="hidden" name="stamp" value={user.stamp} />
      {/* The checkbox says what the account should become; unchecked means "deactivate". */}
      <input type="hidden" name={user.isActive ? undefined : 'activate'} value="on" />
      <FormSection card title={t.stateTitle} hint={user.isActive ? t.deactivateHint : undefined}>
        <p>{user.isActive ? t.active : t.inactive}</p>
        {isSelf && <p className="ui-hint">{t.me}</p>}
        <FormActions>
          <SubmitButton variant={user.isActive ? 'danger' : 'primary'}>{user.isActive ? t.deactivate : t.activate}</SubmitButton>
        </FormActions>
      </FormSection>
    </Form>
  );
}

export function ResetPasswordForm({ user }: { user: StaffUser }) {
  const [state, action] = useActionState(resetStaffPasswordAction, INITIAL_FORM_STATE);
  return (
    <Form action={action} errors={state.errors} banner={<FormBanner state={state} />}>
      <input type="hidden" name="id" value={user.id} />
      <input type="hidden" name="stamp" value={user.stamp} />
      <FormSection card title={t.resetTitle} hint={t.resetHint}>
        <FormGrid>
          <TextField name="newPassword" label={t.newPassword} hint={t.passwordHint} type="password" ltr autoComplete="new-password" maxLength={128} required />
        </FormGrid>
        <StepUpSection error={state.errors.currentPassword} />
        <FormActions>
          <SubmitButton>{t.reset}</SubmitButton>
        </FormActions>
      </FormSection>
    </Form>
  );
}
