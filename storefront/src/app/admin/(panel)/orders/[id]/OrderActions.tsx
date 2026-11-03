'use client';

import { useActionState, useRef } from 'react';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { CheckField, Form, FormActions, FormSection, SubmitButton } from '@/components/form';
import { INITIAL_FORM_STATE } from '@/lib/admin/form-state';
import { adminMessages } from '@/lib/admin/messages';
import { messages } from '@/lib/messages';
import type { OrderStatus, PaymentMethod, PaymentStatus } from '@/lib/store-types';
import { FormBanner } from '../../../_components/FormBits';
import { advanceStatusAction, cancelOrderAction, confirmPaymentAction } from '../actions';
import styles from '../../../admin.module.css';

interface Props {
  id: number;
  status: OrderStatus;
  paymentStatus: PaymentStatus;
  paymentMethod: PaymentMethod;
}

const NEXT: Partial<Record<OrderStatus, 'confirmed' | 'shipped' | 'delivered'>> = {
  pending: 'confirmed',
  confirmed: 'shipped',
  shipped: 'delivered',
};

/** The valid next steps only; everything is also checked again on the server. */
export function OrderActions({ id, status, paymentStatus, paymentMethod }: Props) {
  const [confirmState, confirmAction] = useActionState(confirmPaymentAction, INITIAL_FORM_STATE);
  const [advanceState, advanceAction] = useActionState(advanceStatusAction, INITIAL_FORM_STATE);
  const [cancelState, cancelAction] = useActionState(cancelOrderAction, INITIAL_FORM_STATE);
  const dialog = useRef<HTMLDialogElement>(null);

  const canConfirmPayment = paymentMethod === 'cardToCard' && status === 'awaitingPayment';
  const next = NEXT[status];
  const canCancel = status === 'awaitingPayment' || status === 'pending' || status === 'confirmed';
  const paid = paymentStatus === 'paid';

  if (!canConfirmPayment && !next && !canCancel) return null;

  return (
    <FormSection card title={adminMessages.orders.detail}>
      {canConfirmPayment && (
        <Form action={confirmAction} errors={confirmState.errors} banner={<FormBanner state={confirmState} />}>
          <input type="hidden" name="id" value={id} />
          <FormSection hint={adminMessages.orders.confirmPaymentHint}>
            <FormActions>
              <SubmitButton>{adminMessages.orders.confirmPayment}</SubmitButton>
            </FormActions>
          </FormSection>
        </Form>
      )}

      {next && (
        <Form action={advanceAction} errors={advanceState.errors} banner={<FormBanner state={advanceState} />}>
          <input type="hidden" name="id" value={id} />
          <input type="hidden" name="status" value={next} />
          <FormActions>
            <SubmitButton>{adminMessages.orders.advanceTo(messages.statuses[next])}</SubmitButton>
          </FormActions>
        </Form>
      )}

      {canCancel && (
        <>
          <FormActions>
            <Button type="button" variant="danger" onClick={() => dialog.current?.showModal()}>
              {adminMessages.orders.cancelOrder}
            </Button>
          </FormActions>
          <dialog ref={dialog} className={styles.dialog} aria-label={adminMessages.orders.cancelOrder}>
            <Form action={cancelAction} errors={cancelState.errors} banner={<FormBanner state={cancelState} />}>
              <input type="hidden" name="id" value={id} />
              <FormSection title={adminMessages.orders.cancelOrder} hint={adminMessages.orders.cancelWarning}>
                {paid && (
                  <>
                    <Alert variant="warning">{adminMessages.orders.cancelPaidWarning}</Alert>
                    <CheckField name="acknowledge" label={adminMessages.orders.acknowledgeRefund} />
                  </>
                )}
                <FormActions>
                  <SubmitButton variant="danger">{adminMessages.orders.confirmCancel}</SubmitButton>
                  <Button type="button" variant="secondary" onClick={() => dialog.current?.close()}>
                    {adminMessages.cancel}
                  </Button>
                </FormActions>
              </FormSection>
            </Form>
          </dialog>
        </>
      )}
    </FormSection>
  );
}
