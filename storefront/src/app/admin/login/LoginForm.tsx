'use client';

import { useActionState } from 'react';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { adminMessages } from '@/lib/admin/messages';
import { loginAction, type LoginState } from './actions';
import styles from '../admin.module.css';

const INITIAL: LoginState = { error: null };

export function LoginForm() {
  const [state, action, pending] = useActionState(loginAction, INITIAL);
  return (
    <form action={action} className={styles.loginForm}>
      {state.error && <Alert variant="danger">{state.error}</Alert>}
      <label className={styles.field}>
        <span>{adminMessages.username}</span>
        <input className="ui-control" name="username" type="text" autoComplete="username" autoCapitalize="none" spellCheck={false} dir="ltr" maxLength={256} required />
      </label>
      <label className={styles.field}>
        <span>{adminMessages.password}</span>
        <input className="ui-control" name="password" type="password" autoComplete="current-password" dir="ltr" required />
      </label>
      <Button type="submit" loading={pending} block>
        {adminMessages.login}
      </Button>
    </form>
  );
}
