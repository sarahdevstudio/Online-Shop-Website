'use client';

import { useActionState, useRef } from 'react';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { INITIAL_FORM_STATE } from '@/lib/admin/form-state';
import { adminMessages } from '@/lib/admin/messages';
import { messages } from '@/lib/messages';
import type { OrderStatus, PaymentMethod, PaymentStatus } from '@/lib/store-types';
import { FormBanner, SubmitButton } from '../../../_components/FormBits';
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
    <section className={styles.panel} aria-labelledby="actions">
      <h2 id="actions" className="ui-heading-4">
        {adminMessages.orders.detail}
      </h2>

      {canConfirmPayment && (
        <form action={confirmAction} className={styles.grid2}>
          <input type="hidden" name="id" value={id} />
          <FormBanner state={confirmState} />
          <p className={styles.hint}>{adminMessages.orders.confirmPaymentHint}</p>
          <div>
            <SubmitButton>{adminMessages.orders.confirmPayment}</SubmitButton>
          </div>
        </form>
      )}

      {next && (
        <form action={advanceAction} className={styles.row}>
          <input type="hidden" name="id" value={id} />
          <input type="hidden" name="status" value={next} />
          <FormBanner state={advanceState} />
          <SubmitButton>{adminMessages.orders.advanceTo(messages.statuses[next])}</SubmitButton>
        </form>
      )}

      {canCancel && (
        <>
          <div>
            <Button type="button" variant="danger" onClick={() => dialog.current?.showModal()}>
              {adminMessages.orders.cancelOrder}
            </Button>
          </div>
          <dialog ref={dialog} className={styles.dialog} aria-labelledby="cancel-title">
            <form action={cancelAction} className={styles.grid2}>
              <input type="hidden" name="id" value={id} />
              <h3 id="cancel-title" className="ui-heading-4">
                {adminMessages.orders.cancelOrder}
              </h3>
              <p>{adminMessages.orders.cancelWarning}</p>
              {paid && (
                <>
                  <Alert variant="warning">{adminMessages.orders.cancelPaidWarning}</Alert>
                  <label className={styles.checkRow}>
                    <input type="checkbox" name="acknowledge" required /> {adminMessages.orders.acknowledgeRefund}
                  </label>
                </>
              )}
              <FormBanner state={cancelState} />
              <div className={styles.row}>
                <SubmitButton variant="danger">{adminMessages.orders.confirmCancel}</SubmitButton>
                <Button type="button" variant="secondary" onClick={() => dialog.current?.close()}>
                  {adminMessages.cancel}
                </Button>
              </div>
            </form>
          </dialog>
        </>
      )}
    </section>
  );
}
