'use client';

import { useRouter } from 'next/navigation';
import { useFormStatus } from 'react-dom';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { adminMessages } from '@/lib/admin/messages';
import type { FormState } from '@/lib/admin/form-state';
import styles from '../admin.module.css';

/** Submit button that shows a pending state while its form's action runs. */
export function SubmitButton({ children, variant = 'primary', size }: { children: React.ReactNode; variant?: 'primary' | 'secondary' | 'danger' | 'ghost'; size?: 'sm' | 'md' | 'lg' }) {
  const { pending } = useFormStatus();
  return (
    <Button type="submit" variant={variant} size={size} loading={pending}>
      {children}
    </Button>
  );
}

/**
 * Result of a form action: success text, error text, or - for a 409 "data changed" - a banner with a Reload button and the
 * values the admin had typed, so nothing they entered is silently lost or silently overwritten.
 */
export function FormBanner({ state }: { state: FormState }) {
  const router = useRouter();
  if (state.conflict) {
    return (
      <Alert variant="warning" heading={adminMessages.conflict}>
        {Object.keys(state.submitted).length > 0 && (
          <>
            <p>{adminMessages.conflictYourValues}</p>
            <ul>
              {Object.entries(state.submitted)
                .filter(([key]) => !key.endsWith('rowVersion') && !/^(id|productId|variantId|imageId)$/.test(key))
                .map(([key, value]) => (
                  <li key={key}>
                    <bdi>{key}</bdi>: <bdi>{value || adminMessages.none}</bdi>
                  </li>
                ))}
            </ul>
          </>
        )}
        <Button type="button" size="sm" onClick={() => router.refresh()}>
          {adminMessages.reload}
        </Button>
      </Alert>
    );
  }
  if (state.message) return <Alert variant={state.ok ? 'success' : 'danger'}>{state.message}</Alert>;
  return null;
}

interface FieldProps {
  name: string;
  label: string;
  state: FormState;
  defaultValue?: string | number | null;
  hint?: string;
  type?: 'text' | 'number' | 'password' | 'email' | 'tel';
  ltr?: boolean;
  required?: boolean;
  multiline?: boolean;
  wide?: boolean;
  inputMode?: 'numeric' | 'tel' | 'text';
  autoComplete?: string;
  maxLength?: number;
}

export function Field({ name, label, state, defaultValue, hint, type = 'text', ltr, required, multiline, wide, inputMode, autoComplete, maxLength }: FieldProps) {
  const id = `f-${name}`;
  const error = state.errors[name];
  const common = {
    id,
    name,
    defaultValue: defaultValue ?? '',
    className: `ui-control ${ltr ? styles.ltr : ''}`,
    required,
    'aria-invalid': error ? true : undefined,
    'aria-describedby': error ? `${id}-error` : hint ? `${id}-hint` : undefined,
    autoComplete,
    maxLength,
  };
  return (
    <div className={`${styles.field} ${wide ? styles.wide : ''}`}>
      <label htmlFor={id} className={styles.fieldLabel}>
        {label}
      </label>
      {multiline ? <textarea {...common} rows={4} /> : <input {...common} type={type} inputMode={inputMode} />}
      {hint && !error && (
        <span id={`${id}-hint`} className={styles.hint}>
          {hint}
        </span>
      )}
      {error && (
        <span id={`${id}-error`} className={styles.error}>
          {error}
        </span>
      )}
    </div>
  );
}

export function CheckField({ name, label, defaultChecked }: { name: string; label: string; defaultChecked?: boolean }) {
  return (
    <label className={styles.checkRow}>
      <input type="checkbox" name={name} defaultChecked={defaultChecked} /> {label}
    </label>
  );
}
