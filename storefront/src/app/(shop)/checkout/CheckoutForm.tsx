'use client';

import { useActionState } from 'react';
import { Alert } from '@/components/Alert';
import { ChoiceGroup, Form, FormActions, FormGrid, FormSection, RequiredNote, SubmitButton, TextAreaField, TextField } from '@/components/form';
import { INITIAL_CHECKOUT_STATE } from '@/lib/checkout-form';
import { messages } from '@/lib/messages';
import type { PaymentMethod } from '@/lib/store-types';
import { placeOrderAction } from './actions';

interface CheckoutFormProps {
  idempotencyKey: string;
  expectedTotal: number;
  methods: readonly PaymentMethod[];
}

/**
 * The checkout form. A normal `<form>` whose action is a Server Action: it submits and shows errors without JavaScript;
 * with JavaScript the button also shows a pending state.
 */
export function CheckoutForm({ idempotencyKey, expectedTotal, methods }: CheckoutFormProps) {
  const [state, formAction] = useActionState(placeOrderAction, INITIAL_CHECKOUT_STATE);
  const { values, errors } = state;
  const selected = values.paymentMethod || (methods.length === 1 ? methods[0] : '');
  const options = [
    ...(methods.includes('cardToCard') ? [{ value: 'cardToCard', title: messages.cardToCard, description: messages.cardToCardHint }] : []),
    ...(methods.includes('cashOnDelivery') ? [{ value: 'cashOnDelivery', title: messages.cashOnDelivery, description: messages.cashOnDeliveryHint }] : []),
  ];

  return (
    <Form action={formAction} errors={errors} banner={state.formError && Object.keys(errors).length === 0 ? <Alert variant="danger">{state.formError}</Alert> : null}>
      {/* Same key for every submit of this page: a double click can only ever create one order. */}
      <input type="hidden" name="idempotencyKey" value={idempotencyKey} />
      <input type="hidden" name="expectedTotal" value={expectedTotal} />
      <RequiredNote />

      <FormSection title={messages.paymentMethod}>
        <ChoiceGroup name="paymentMethod" legend={messages.paymentMethod} hideLegend options={options} defaultValue={selected} required />
      </FormSection>

      <FormSection title={messages.shippingDetails}>
        <FormGrid>
          <TextField name="fullName" label={messages.fullName} defaultValue={values.fullName} autoComplete="name" required />
          <TextField name="mobile" label={messages.mobile} hint={messages.mobileHint} defaultValue={values.mobile} type="tel" inputMode="tel" ltr autoComplete="tel" required />
          <TextField name="province" label={messages.province} defaultValue={values.province} autoComplete="address-level1" required />
          <TextField name="city" label={messages.city} defaultValue={values.city} autoComplete="address-level2" required />
          <TextAreaField name="address" label={messages.address} defaultValue={values.address} rows={3} autoComplete="street-address" required />
          <TextField name="postalCode" label={messages.postalCode} defaultValue={values.postalCode} inputMode="numeric" ltr autoComplete="postal-code" maxLength={10} required />
          <TextAreaField name="note" label={messages.note} defaultValue={values.note} rows={3} />
        </FormGrid>
      </FormSection>

      <FormActions stretch>
        <SubmitButton size="lg" pendingLabel={messages.placingOrder}>
          {messages.placeOrder}
        </SubmitButton>
      </FormActions>
    </Form>
  );
}
