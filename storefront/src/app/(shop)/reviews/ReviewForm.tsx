'use client';

import { useActionState } from 'react';
import { Alert } from '@/components/Alert';
import { ChoiceGroup, Form, FormActions, FormGrid, RequiredNote, SubmitButton, TextAreaField, TextField } from '@/components/form';
import { formatNumber } from '@/lib/format';
import { reviewMessages as t } from '@/lib/review-messages';
import { INITIAL_REVIEW_STATE } from '@/lib/review-form';
import { submitReviewAction } from './actions';

/**
 * The guest review form: a normal `<form>` posting to a Server Action (works without JavaScript). Name, mobile number, 1–5 stars and
 * a text. The extra field `website` is hidden from people (a honeypot for scripts).
 */
export function ReviewForm({ productSlug }: { productSlug: string }) {
  const [state, action] = useActionState(submitReviewAction, INITIAL_REVIEW_STATE);
  const { values, errors } = state;
  const options = [5, 4, 3, 2, 1].map((n) => ({ value: String(n), title: t.ratingOption(formatNumber(n)) }));
  return (
    <Form
      action={action}
      errors={errors}
      banner={state.message ? <Alert variant={state.ok ? 'success' : 'danger'}>{state.message}</Alert> : null}
    >
      <input type="hidden" name="productSlug" value={productSlug} />
      {/* Honeypot: off-screen for everyone, never reachable by keyboard or screen reader, tempting for a script. */}
      <div aria-hidden="true" style={{ position: 'absolute', insetInlineStart: '-10000px', blockSize: 0, overflow: 'hidden' }}>
        <label>
          {t.website}
          <input type="text" name="website" tabIndex={-1} autoComplete="off" defaultValue="" />
        </label>
      </div>
      <RequiredNote />
      <FormGrid>
        <TextField name="name" label={t.name} defaultValue={values.name} autoComplete="name" maxLength={60} required />
        <TextField name="mobile" label={t.mobile} defaultValue={values.mobile} type="tel" inputMode="tel" ltr autoComplete="tel" required />
        <ChoiceGroup name="rating" legend={t.rating} options={options} defaultValue={values.rating} required />
        <TextAreaField name="text" label={t.text} hint={t.textHint} defaultValue={values.text} rows={4} maxLength={2000} required />
      </FormGrid>
      <p className="ui-hint">{t.hint}</p>
      <FormActions>
        <SubmitButton>{t.submit}</SubmitButton>
      </FormActions>
    </Form>
  );
}
