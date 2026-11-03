'use client';

import { useActionState } from 'react';
import { CheckField, Form, FormActions, FormBlock, FormGrid, FormSection, RequiredNote, SubmitButton, TextField } from '@/components/form';
import { INITIAL_FORM_STATE } from '@/lib/admin/form-state';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminBrand } from '@/lib/admin/types';
import { FormBanner } from '../../_components/FormBits';
import { deleteBrandAction, saveBrandAction } from './actions';

/** Create (no `brand`, with a `title`) or edit a brand. */
export function BrandForm({ brand, title }: { brand?: AdminBrand; title?: string }) {
  const [state, action] = useActionState(saveBrandAction, INITIAL_FORM_STATE);
  return (
    <Form action={action} errors={state.errors} banner={<FormBanner state={state} />}>
      {brand && <input type="hidden" name="id" value={brand.id} />}
      <FormSection card={Boolean(title)} title={title}>
        <RequiredNote />
        <FormGrid>
          <TextField name="name" label={adminMessages.products.name} defaultValue={brand?.name} required />
          {brand ? (
            <FormBlock title={adminMessages.products.slug} full={false}>
              <bdi dir="ltr">{brand.slug}</bdi>
            </FormBlock>
          ) : (
            <TextField name="slug" label={adminMessages.products.slug} hint={adminMessages.products.slugHint} ltr required />
          )}
          <CheckField name="isActive" label={adminMessages.active} defaultChecked={brand?.isActive ?? true} full />
        </FormGrid>
        <FormActions>
          <SubmitButton>{brand ? adminMessages.save : adminMessages.create}</SubmitButton>
        </FormActions>
      </FormSection>
    </Form>
  );
}

export function DeleteBrandForm({ id }: { id: number }) {
  const [state, action] = useActionState(deleteBrandAction, INITIAL_FORM_STATE);
  return (
    <Form action={action} errors={state.errors} banner={<FormBanner state={state} />}>
      <input type="hidden" name="id" value={id} />
      <FormActions>
        <SubmitButton variant="danger">{adminMessages.delete}</SubmitButton>
      </FormActions>
    </Form>
  );
}
