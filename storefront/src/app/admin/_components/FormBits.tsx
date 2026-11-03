'use client';

import { useRouter } from 'next/navigation';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { adminMessages } from '@/lib/admin/messages';
import type { FormState } from '@/lib/admin/form-state';

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
  // Field errors are listed by the form's own error summary; a banner would repeat its heading.
  if (!state.ok && Object.keys(state.errors).length > 0) return null;
  if (state.message) return <Alert variant={state.ok ? 'success' : 'danger'}>{state.message}</Alert>;
  return null;
}
