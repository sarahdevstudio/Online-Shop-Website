'use client';

import { useActionState } from 'react';
import { Alert } from '@/components/Alert';
import { Form, FormActions, RequiredNote, SubmitButton, TextField } from '@/components/form';
import { messages } from '@/lib/messages';
import { INITIAL_TRANSFER_STATE } from '@/lib/transfer-form';
import { submitTransferReferenceAction } from './actions';

/** Works without JavaScript (a normal POST that re-renders with the result); with it, the button shows a pending state. */
export function TransferReferenceForm({ token }: { token: string }) {
  const [state, formAction] = useActionState(submitTransferReferenceAction, INITIAL_TRANSFER_STATE);
  const banner = (
    <>
      {state.saved && <Alert variant="success">{messages.transferSaved}</Alert>}
      {state.error && <Alert variant="danger">{state.error}</Alert>}
    </>
  );

  return (
    <Form action={formAction} errors={state.errors} banner={banner}>
      <input type="hidden" name="token" value={token} />
      <RequiredNote />
      <TextField name="tracking" label={messages.transferTracking} defaultValue={state.values.tracking} ltr autoComplete="off" required full />
      <TextField name="time" label={messages.transferTime} defaultValue={state.values.time} autoComplete="off" required full />
      <TextField name="cardLast4" label={messages.transferLast4} defaultValue={state.values.cardLast4} ltr inputMode="numeric" maxLength={4} autoComplete="off" required full />
      <FormActions>
        <SubmitButton>{messages.transferSubmit}</SubmitButton>
      </FormActions>
    </Form>
  );
}
