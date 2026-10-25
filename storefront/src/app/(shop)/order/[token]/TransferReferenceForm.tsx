'use client';

import { useActionState } from 'react';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { messages } from '@/lib/messages';
import { INITIAL_TRANSFER_STATE } from '@/lib/transfer-form';
import { submitTransferReferenceAction } from './actions';
import styles from './order.module.css';

/** Works without JavaScript (a normal POST that re-renders with the result); with it, the button shows a pending state. */
export function TransferReferenceForm({ token }: { token: string }) {
  const [state, formAction, pending] = useActionState(submitTransferReferenceAction, INITIAL_TRANSFER_STATE);

  const field = (name: 'tracking' | 'time' | 'cardLast4', label: string, options: { ltr?: boolean; inputMode?: 'numeric' } = {}) => {
    const error = state.errors[name];
    return (
      <div className={styles.field}>
        <label htmlFor={`transfer-${name}`} className={styles.label}>
          {label}
        </label>
        <input
          id={`transfer-${name}`}
          name={name}
          type="text"
          className={`ui-control ${options.ltr ? styles.ltr : ''}`}
          defaultValue={state.values[name]}
          inputMode={options.inputMode}
          maxLength={name === 'cardLast4' ? 4 : undefined}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? `transfer-${name}-error` : undefined}
          required
        />
        {error && (
          <span id={`transfer-${name}-error`} className={styles.error}>
            {error}
          </span>
        )}
      </div>
    );
  };

  return (
    <form action={formAction} className={styles.form} noValidate>
      <input type="hidden" name="token" value={token} />
      {state.saved && <Alert variant="success">{messages.transferSaved}</Alert>}
      {state.error && <Alert variant="danger">{state.error}</Alert>}
      {field('tracking', messages.transferTracking, { ltr: true })}
      {field('time', messages.transferTime)}
      {field('cardLast4', messages.transferLast4, { ltr: true, inputMode: 'numeric' })}
      <Button type="submit" loading={pending}>
        {messages.transferSubmit}
      </Button>
    </form>
  );
}
