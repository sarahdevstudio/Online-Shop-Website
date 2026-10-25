'use client';

import { useActionState } from 'react';
import { Alert } from '@/components/Alert';
import { INITIAL_FORM_STATE } from '@/lib/admin/form-state';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminSettings } from '@/lib/admin/types';
import { CheckField, Field, FormBanner, SubmitButton } from '../../_components/FormBits';
import { saveSettingsAction } from './actions';
import styles from '../../admin.module.css';

const t = adminMessages.settings;

export function SettingsForm({ data }: { data: AdminSettings }) {
  const [state, action] = useActionState(saveSettingsAction, INITIAL_FORM_STATE);
  const s = data.settings;
  // A refused save (wrong password, validation) re-shows what was typed instead of resetting the form to the stored values.
  const typed = !state.ok && Object.keys(state.submitted).length > 0;
  const v = (name: string, stored: string | number | null) => (typed && name in state.submitted ? state.submitted[name] : stored);
  const on = (name: string, stored: boolean) => (typed ? name in state.submitted : stored);
  return (
    <form action={action} className={styles.grid2}>
      <div className={styles.wide}>
        <FormBanner state={state} />
        <Alert variant={data.cardToCardOffered ? 'success' : 'warning'}>{data.cardToCardOffered ? t.offered : t.notOffered}</Alert>
      </div>

      {/* Fields are keyed by the stored version (fresh values after a save or reload); the banner state is not. */}
      <input type="hidden" name="rowVersion" value={data.rowVersion} />
      <div key={data.rowVersion} style={{ display: 'contents' }}>
      <fieldset className={`${styles.panel} ${styles.wide}`}>
        <legend className="ui-heading-4">{t.shipping}</legend>
        <div className={styles.grid2}>
          <Field name="shippingFlatFee" label={t.shippingFee} state={state} type="text" inputMode="numeric" ltr defaultValue={v('shippingFlatFee', s.shippingFlatFee)} required />
          <Field name="freeShippingThreshold" label={t.freeThreshold} state={state} type="text" inputMode="numeric" ltr defaultValue={v('freeShippingThreshold', s.freeShippingThreshold)} />
          <Field name="maxQuantityPerLine" label={t.maxQty} state={state} type="text" inputMode="numeric" ltr defaultValue={v('maxQuantityPerLine', s.maxQuantityPerLine)} required />
        </div>
      </fieldset>

      <fieldset className={`${styles.panel} ${styles.wide}`}>
        <legend className="ui-heading-4">{t.payment}</legend>
        <div className={styles.grid2}>
          <CheckField name="cardToCardEnabled" label={t.cardToCard} defaultChecked={on('cardToCardEnabled', s.cardToCardEnabled)} />
          <CheckField name="cashOnDeliveryEnabled" label={t.cashOnDelivery} defaultChecked={on('cashOnDeliveryEnabled', s.cashOnDeliveryEnabled)} />
          {state.errors.paymentMethods && <span className={`${styles.error} ${styles.wide}`}>{state.errors.paymentMethods}</span>}
          <Field name="cardNumber" label={t.cardNumber} state={state} ltr inputMode="numeric" defaultValue={v('cardNumber', s.cardNumber)} />
          <Field name="cardHolderName" label={t.cardHolder} state={state} defaultValue={v('cardHolderName', s.cardHolderName)} />
          <Field name="bankName" label={t.bankName} state={state} defaultValue={v('bankName', s.bankName)} />
        </div>
      </fieldset>

      <fieldset className={`${styles.panel} ${styles.wide}`}>
        <legend className="ui-heading-4">{t.contact}</legend>
        <div className={styles.grid2}>
          <Field name="storePhone" label={t.phone} state={state} ltr type="tel" defaultValue={v('storePhone', s.storePhone)} />
          <Field name="instagramHandle" label={t.instagram} state={state} ltr defaultValue={v('instagramHandle', s.instagramHandle)} />
          <Field name="workingHours" label={t.hours} state={state} defaultValue={v('workingHours', s.workingHours)} />
          <Field name="storeAddress" label={t.address} state={state} defaultValue={v('storeAddress', s.storeAddress)} multiline wide />
        </div>
      </fieldset>

      <fieldset className={`${styles.panel} ${styles.wide}`}>
        <legend className="ui-heading-4">{t.stepUpTitle}</legend>
        <p className={styles.hint}>{t.stepUpHint}</p>
        <Field name="currentPassword" label={adminMessages.currentPassword} state={state} type="password" ltr autoComplete="current-password" />
      </fieldset>

      </div>

      <div className={styles.row}>
        <SubmitButton>{adminMessages.save}</SubmitButton>
      </div>
    </form>
  );
}
