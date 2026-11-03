'use client';

import { useActionState } from 'react';
import { Alert } from '@/components/Alert';
import { CheckField, Form, FormActions, FormGrid, FormSection, RequiredNote, SubmitButton, TextAreaField, TextField } from '@/components/form';
import { INITIAL_FORM_STATE } from '@/lib/admin/form-state';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminSettings } from '@/lib/admin/types';
import { FormBanner } from '../../_components/FormBits';
import { saveSettingsAction } from './actions';

const t = adminMessages.settings;

export function SettingsForm({ data }: { data: AdminSettings }) {
  const [state, action] = useActionState(saveSettingsAction, INITIAL_FORM_STATE);
  const s = data.settings;
  // A refused save (wrong password, validation) re-shows what was typed instead of resetting the form to the stored values.
  const typed = !state.ok && Object.keys(state.submitted).length > 0;
  const v = (name: string, stored: string | number | null) => (typed && name in state.submitted ? state.submitted[name] : stored);
  const on = (name: string, stored: boolean) => (typed ? name in state.submitted : stored);
  return (
    <Form
      action={action}
      errors={state.errors}
      banner={
        <>
          <FormBanner state={state} />
          <Alert variant={data.cardToCardOffered ? 'success' : 'warning'}>{data.cardToCardOffered ? t.offered : t.notOffered}</Alert>
        </>
      }
    >
      {/* Fields are keyed by the stored version (fresh values after a save or reload); the banner state is not. */}
      <input type="hidden" name="rowVersion" value={data.rowVersion} />
      <RequiredNote />
      <div key={data.rowVersion} style={{ display: 'contents' }}>
        <FormSection card title={t.shipping}>
          <FormGrid columns={3}>
            <TextField name="shippingFlatFee" label={t.shippingFee} inputMode="numeric" ltr autoComplete="off" defaultValue={v('shippingFlatFee', s.shippingFlatFee)} required />
            <TextField name="freeShippingThreshold" label={t.freeThreshold} hint={t.freeThresholdHint} inputMode="numeric" ltr autoComplete="off" defaultValue={v('freeShippingThreshold', s.freeShippingThreshold)} />
            <TextField name="maxQuantityPerLine" label={t.maxQty} inputMode="numeric" ltr autoComplete="off" defaultValue={v('maxQuantityPerLine', s.maxQuantityPerLine)} required />
          </FormGrid>
        </FormSection>

        <FormSection card title={t.payment}>
          <FormGrid>
            <CheckField name="cardToCardEnabled" label={t.cardToCard} defaultChecked={on('cardToCardEnabled', s.cardToCardEnabled)} error={state.errors.paymentMethods} />
            <CheckField name="cashOnDeliveryEnabled" label={t.cashOnDelivery} defaultChecked={on('cashOnDeliveryEnabled', s.cashOnDeliveryEnabled)} />
          </FormGrid>
          <FormGrid columns={3}>
            <TextField name="cardNumber" label={t.cardNumber} inputMode="numeric" ltr autoComplete="off" defaultValue={v('cardNumber', s.cardNumber)} />
            <TextField name="cardHolderName" label={t.cardHolder} autoComplete="off" defaultValue={v('cardHolderName', s.cardHolderName)} />
            <TextField name="bankName" label={t.bankName} autoComplete="off" defaultValue={v('bankName', s.bankName)} />
          </FormGrid>
        </FormSection>

        <FormSection card title={t.contact}>
          <FormGrid columns={3}>
            <TextField name="storePhone" label={t.phone} type="tel" inputMode="tel" ltr autoComplete="off" defaultValue={v('storePhone', s.storePhone)} />
            <TextField name="instagramHandle" label={t.instagram} hint={t.instagramHint} ltr autoComplete="off" defaultValue={v('instagramHandle', s.instagramHandle)} />
            <TextField name="workingHours" label={t.hours} autoComplete="off" defaultValue={v('workingHours', s.workingHours)} />
            <TextAreaField name="storeAddress" label={t.address} rows={3} defaultValue={v('storeAddress', s.storeAddress) as string | null} />
          </FormGrid>
        </FormSection>

        <FormSection card title={t.stepUpTitle} hint={t.stepUpHint}>
          <FormGrid>
            <TextField name="currentPassword" label={adminMessages.currentPassword} type="password" ltr autoComplete="current-password" />
          </FormGrid>
        </FormSection>
      </div>

      <FormActions>
        <SubmitButton>{adminMessages.save}</SubmitButton>
      </FormActions>
    </Form>
  );
}
