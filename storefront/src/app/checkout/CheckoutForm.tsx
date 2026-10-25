'use client';

import { useActionState } from 'react';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { INITIAL_CHECKOUT_STATE, type CheckoutField } from '@/lib/checkout-form';
import { messages } from '@/lib/messages';
import type { PaymentMethod } from '@/lib/store-types';
import { placeOrderAction } from './actions';
import styles from './checkout.module.css';

interface CheckoutFormProps {
  idempotencyKey: string;
  expectedTotal: number;
  methods: readonly PaymentMethod[];
}

interface FieldProps {
  name: CheckoutField;
  label: string;
  hint?: string;
  error?: string;
  value: string;
  wide?: boolean;
  multiline?: boolean;
  inputMode?: 'tel' | 'numeric';
  ltr?: boolean;
  autoComplete?: string;
  required?: boolean;
}

function Field({ name, label, hint, error, value, wide, multiline, inputMode, ltr, autoComplete, required = true }: FieldProps) {
  const id = `checkout-${name}`;
  const describedBy = [hint ? `${id}-hint` : null, error ? `${id}-error` : null].filter(Boolean).join(' ') || undefined;
  const common = {
    id,
    name,
    defaultValue: value,
    className: `ui-control ${ltr ? styles.ltr : ''}`,
    'aria-invalid': error ? true : undefined,
    'aria-describedby': describedBy,
    autoComplete,
    required,
  };
  return (
    <div className={`${styles.field} ${wide ? styles.wide : ''}`}>
      <label htmlFor={id} className={styles.label}>
        {label}
      </label>
      {multiline ? <textarea {...common} rows={3} /> : <input {...common} type="text" inputMode={inputMode} />}
      {hint && (
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

/**
 * The checkout form. A normal `<form>` whose action is a Server Action: it submits and shows errors without JavaScript;
 * with JavaScript the button also shows a pending state.
 */
export function CheckoutForm({ idempotencyKey, expectedTotal, methods }: CheckoutFormProps) {
  const [state, formAction, pending] = useActionState(placeOrderAction, INITIAL_CHECKOUT_STATE);
  const { values, errors } = state;
  const selected = values.paymentMethod || (methods.length === 1 ? methods[0] : '');

  return (
    <form action={formAction} className={styles.form} noValidate>
      {/* Same key for every submit of this page: a double click can only ever create one order. */}
      <input type="hidden" name="idempotencyKey" value={idempotencyKey} />
      <input type="hidden" name="expectedTotal" value={expectedTotal} />

      {state.formError && <Alert variant="danger">{state.formError}</Alert>}

      <fieldset className={styles.methods}>
        <legend className="ui-heading-4">{messages.paymentMethod}</legend>
        {methods.includes('cardToCard') && (
          <label className={styles.method}>
            <input type="radio" name="paymentMethod" value="cardToCard" defaultChecked={selected === 'cardToCard'} required />
            <span className={styles.methodTitle}>{messages.cardToCard}</span>
            <span className={styles.hint}>{messages.cardToCardHint}</span>
          </label>
        )}
        {methods.includes('cashOnDelivery') && (
          <label className={styles.method}>
            <input type="radio" name="paymentMethod" value="cashOnDelivery" defaultChecked={selected === 'cashOnDelivery'} required />
            <span className={styles.methodTitle}>{messages.cashOnDelivery}</span>
            <span className={styles.hint}>{messages.cashOnDeliveryHint}</span>
          </label>
        )}
        {errors.paymentMethod && <span className={styles.error}>{errors.paymentMethod}</span>}
      </fieldset>

      <fieldset className={styles.methods}>
        <legend className="ui-heading-4">{messages.shippingDetails}</legend>
        <div className={styles.grid}>
          <Field name="fullName" label={messages.fullName} value={values.fullName} error={errors.fullName} autoComplete="name" />
          <Field
            name="mobile"
            label={messages.mobile}
            hint={messages.mobileHint}
            value={values.mobile}
            error={errors.mobile}
            inputMode="tel"
            ltr
            autoComplete="tel"
          />
          <Field name="province" label={messages.province} value={values.province} error={errors.province} autoComplete="address-level1" />
          <Field name="city" label={messages.city} value={values.city} error={errors.city} autoComplete="address-level2" />
          <Field name="address" label={messages.address} value={values.address} error={errors.address} wide multiline autoComplete="street-address" />
          <Field
            name="postalCode"
            label={messages.postalCode}
            value={values.postalCode}
            error={errors.postalCode}
            inputMode="numeric"
            ltr
            autoComplete="postal-code"
          />
          <Field name="note" label={messages.note} value={values.note} error={errors.note} wide multiline required={false} />
        </div>
      </fieldset>

      <Button type="submit" size="lg" loading={pending} block>
        {pending ? messages.placingOrder : messages.placeOrder}
      </Button>
    </form>
  );
}
