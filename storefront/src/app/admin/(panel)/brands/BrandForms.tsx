'use client';

import { useActionState } from 'react';
import { INITIAL_FORM_STATE } from '@/lib/admin/form-state';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminBrand } from '@/lib/admin/types';
import { CheckField, Field, FormBanner, SubmitButton } from '../../_components/FormBits';
import { deleteBrandAction, saveBrandAction } from './actions';
import styles from '../../admin.module.css';

export function BrandForm({ brand }: { brand?: AdminBrand }) {
  const [state, action] = useActionState(saveBrandAction, INITIAL_FORM_STATE);
  return (
    <form action={action} className={styles.grid2}>
      {brand && <input type="hidden" name="id" value={brand.id} />}
      <div className={styles.wide}>
        <FormBanner state={state} />
      </div>
      <Field name="name" label={adminMessages.products.name} state={state} defaultValue={brand?.name} required />
      {brand ? (
        <p className={`${styles.ltr} ${styles.muted}`}>{brand.slug}</p>
      ) : (
        <Field name="slug" label={adminMessages.products.slug} hint={adminMessages.products.slugHint} state={state} ltr required />
      )}
      <CheckField name="isActive" label={adminMessages.active} defaultChecked={brand?.isActive ?? true} />
      <div className={styles.row}>
        <SubmitButton>{brand ? adminMessages.save : adminMessages.create}</SubmitButton>
      </div>
    </form>
  );
}

export function DeleteBrandForm({ id }: { id: number }) {
  const [state, action] = useActionState(deleteBrandAction, INITIAL_FORM_STATE);
  return (
    <form action={action}>
      <input type="hidden" name="id" value={id} />
      <FormBanner state={state} />
      <SubmitButton variant="danger" size="sm">
        {adminMessages.delete}
      </SubmitButton>
    </form>
  );
}
