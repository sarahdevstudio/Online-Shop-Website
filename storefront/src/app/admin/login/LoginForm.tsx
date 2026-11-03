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
      <div className={styles.field}>
        <label htmlFor="login-username" className={styles.fieldLabel}>
          {adminMessages.username}
        </label>
        <input id="login-username" className="ui-control" name="username" type="text" autoComplete="username" autoCapitalize="none" spellCheck={false} dir="ltr" maxLength={256} required />
      </div>
      <div className={styles.field}>
        <label htmlFor="login-password" className={styles.fieldLabel}>
          {adminMessages.password}
        </label>
        <input id="login-password" className="ui-control" name="password" type="password" autoComplete="current-password" dir="ltr" required />
      </div>
      <Button type="submit" loading={pending} block>
        {adminMessages.login}
      </Button>
    </form>
  );
}
