'use client';

import type { ReactNode } from 'react';
import { useFormStatus } from 'react-dom';
import { Button } from '@/components/Button';

/** Submit button that shows a pending state (spinner, aria-busy, ignores clicks) while its form's action runs. */
export function SubmitButton({
  children,
  pendingLabel,
  variant = 'primary',
  size,
  block,
  disabled,
  'aria-label': ariaLabel,
  name,
  value,
}: {
  children: ReactNode;
  pendingLabel?: ReactNode;
  variant?: 'primary' | 'secondary' | 'danger' | 'ghost';
  size?: 'sm' | 'md' | 'lg';
  block?: boolean;
  disabled?: boolean;
  'aria-label'?: string;
  /** Sent with the form when this button submits it (several decisions on one form). */
  name?: string;
  value?: string;
}) {
  const { pending } = useFormStatus();
  return (
    <Button type="submit" variant={variant} size={size} block={block} loading={pending} disabled={disabled} aria-label={ariaLabel} name={name} value={value}>
      {pending && pendingLabel ? pendingLabel : children}
    </Button>
  );
}
