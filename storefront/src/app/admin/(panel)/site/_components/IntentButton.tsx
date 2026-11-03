'use client';

import type { ReactNode } from 'react';
import { useFormStatus } from 'react-dom';
import { Button } from '@/components/Button';

/** A submit button that tells the Server Action which button was used (`intent`) and shows pending only for itself. */
export function IntentButton({ intent, variant = 'secondary', children }: { intent: string; variant?: 'primary' | 'secondary' | 'danger' | 'ghost'; children: ReactNode }) {
  const { pending, data } = useFormStatus();
  const mine = pending && data?.get('intent') === intent;
  return (
    <Button type="submit" name="intent" value={intent} variant={variant} loading={mine} disabled={pending && !mine}>
      {children}
    </Button>
  );
}
