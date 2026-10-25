'use client';

import { useActionState } from 'react';
import { INITIAL_FORM_STATE } from '@/lib/admin/form-state';
import { adminMessages } from '@/lib/admin/messages';
import { Field, FormBanner, SubmitButton } from '../../_components/FormBits';
import { changePasswordAction } from './actions';
import styles from '../../admin.module.css';

export function ChangePasswordForm() {
  const [state, action] = useActionState(changePasswordAction, INITIAL_FORM_STATE);
  return (
    <form action={action} className={styles.panel}>
      <h2 className="ui-heading-4">{adminMessages.changePassword}</h2>
      <FormBanner state={state} />
      <Field name="currentPassword" label={adminMessages.currentPassword} state={state} type="password" ltr autoComplete="current-password" required />
      <Field
        name="newPassword"
        label={adminMessages.newPassword}
        hint={adminMessages.changePasswordHint}
        state={state}
        type="password"
        ltr
        autoComplete="new-password"
        required
      />
      <div>
        <SubmitButton>{adminMessages.changePassword}</SubmitButton>
      </div>
    </form>
  );
}
