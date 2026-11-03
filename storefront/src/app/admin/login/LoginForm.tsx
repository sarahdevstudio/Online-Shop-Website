'use client';

import { useActionState } from 'react';
import { Alert } from '@/components/Alert';
import { Form, FormActions, SubmitButton, TextField } from '@/components/form';
import { adminMessages } from '@/lib/admin/messages';
import { loginAction, type LoginState } from './actions';

const INITIAL: LoginState = { error: null, errors: {} };

export function LoginForm() {
  const [state, action] = useActionState(loginAction, INITIAL);
  return (
    <Form action={action} errors={state.errors} banner={state.error ? <Alert variant="danger">{state.error}</Alert> : null}>
      <TextField name="username" label={adminMessages.username} ltr autoComplete="username" maxLength={256} required full />
      <TextField name="password" label={adminMessages.password} type="password" ltr autoComplete="current-password" required full />
      <FormActions stretch>
        <SubmitButton block>{adminMessages.login}</SubmitButton>
      </FormActions>
    </Form>
  );
}
