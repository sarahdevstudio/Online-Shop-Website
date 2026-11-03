'use client';

import { useActionState } from 'react';
import { Form, FormActions, FormGrid, FormSection, RequiredNote, SubmitButton, TextField } from '@/components/form';
import { INITIAL_FORM_STATE } from '@/lib/admin/form-state';
import { adminMessages } from '@/lib/admin/messages';
import { FormBanner } from '../../_components/FormBits';
import { changePasswordAction } from './actions';

export function ChangePasswordForm() {
  const [state, action] = useActionState(changePasswordAction, INITIAL_FORM_STATE);
  return (
    <Form action={action} errors={state.errors} banner={<FormBanner state={state} />}>
      <FormSection card title={adminMessages.changePassword}>
        <RequiredNote />
        <FormGrid>
          <TextField name="currentPassword" label={adminMessages.currentPassword} type="password" ltr autoComplete="current-password" required />
          <TextField name="newPassword" label={adminMessages.newPassword} hint={adminMessages.changePasswordHint} type="password" ltr autoComplete="new-password" required />
        </FormGrid>
        <FormActions>
          <SubmitButton>{adminMessages.changePassword}</SubmitButton>
        </FormActions>
      </FormSection>
    </Form>
  );
}
