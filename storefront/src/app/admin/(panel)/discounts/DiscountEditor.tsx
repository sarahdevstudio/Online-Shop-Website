'use client';

import { useActionState, useState } from 'react';
import { INITIAL_FORM_STATE } from '@/lib/admin/form-state';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminDiscount, DiscountType } from '@/lib/admin/types';
import { CheckField, Form, FormActions, FormGrid, FormSection, RequiredNote, SelectField, SubmitButton, TextField } from '@/components/form';
import { FormBanner } from '../../_components/FormBits';
import { deleteDiscountAction, saveDiscountAction } from './actions';

const t = adminMessages.discounts;

/** Create (no `discount`) or edit a discount code: percent or fixed amount off, optional minimum, usage limits, expiry. */
export function DiscountEditor({ discount }: { discount?: AdminDiscount }) {
  const [state, action] = useActionState(saveDiscountAction, INITIAL_FORM_STATE);
  const [type, setType] = useState<DiscountType>(discount?.type ?? 'percent');

  return (
    <Form action={action} errors={state.errors} banner={<FormBanner state={state} />}>
      {discount && <input type="hidden" name="id" value={discount.id} />}
      {discount && <input type="hidden" name="rowVersion" value={discount.rowVersion} />}
      <RequiredNote />

      <FormGrid>
        {!discount && <TextField name="code" label={t.code} hint={t.codeHint} ltr required maxLength={30} />}

        <SelectField name="type" label={t.type} value={type} onChange={(e) => setType(e.target.value as DiscountType)}>
          <option value="percent">{t.typePercent}</option>
          <option value="fixed">{t.typeFixed}</option>
        </SelectField>

        <TextField
          name="value"
          label={t.value}
          hint={type === 'percent' ? t.valuePercentHint : t.valueFixedHint}
          defaultValue={discount?.value}
          required
          ltr
          inputMode="numeric"
        />

        <TextField name="minSubtotal" label={t.minSubtotal} hint={t.minSubtotalHint} defaultValue={discount?.minSubtotal ?? ''} ltr inputMode="numeric" />
        <TextField name="usageLimit" label={t.usageLimit} hint={t.usageLimitHint} defaultValue={discount?.usageLimit ?? ''} ltr inputMode="numeric" />
        <TextField name="perMobileLimit" label={t.perMobileLimit} hint={t.perMobileLimitHint} defaultValue={discount?.perMobileLimit ?? 1} required ltr inputMode="numeric" />

        <div className="ui-field">
          <label htmlFor="expiresAt" className="ui-label">
            {t.expiresAt}
          </label>
          <input id="expiresAt" type="date" name="expiresAt" className="ui-control" defaultValue={discount?.expiresAtUtc?.slice(0, 10) ?? ''} dir="ltr" />
          <p className="ui-hint">{t.expiresAtHint}</p>
        </div>

        <CheckField name="isActive" label={t.active} defaultChecked={discount?.isActive ?? true} full />
      </FormGrid>

      <FormActions>
        <SubmitButton>{adminMessages.save}</SubmitButton>
      </FormActions>
    </Form>
  );
}

/** Deleting is irreversible, so it sits behind a disclosure with a warning. */
export function DeleteDiscountForm({ id }: { id: number }) {
  const [state, action] = useActionState(deleteDiscountAction, INITIAL_FORM_STATE);
  return (
    <details>
      <summary>{t.deleteTitle}</summary>
      <Form action={action} errors={state.errors} banner={<FormBanner state={state} />}>
        <input type="hidden" name="id" value={id} />
        <FormSection hint={t.deleteWarning}>
          <FormActions>
            <SubmitButton variant="danger">{t.deleteConfirm}</SubmitButton>
          </FormActions>
        </FormSection>
      </Form>
    </details>
  );
}
