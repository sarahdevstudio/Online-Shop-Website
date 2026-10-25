'use client';

import { useActionState, useState } from 'react';
import { Button } from '@/components/Button';
import { INITIAL_FORM_STATE } from '@/lib/admin/form-state';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminBrand, AdminCategory, AdminProduct } from '@/lib/admin/types';
import { formatNumber } from '@/lib/format';
import { Field, FormBanner, SubmitButton } from '../../_components/FormBits';
import { createProductAction, updateProductAction } from './actions';
import { GeneralFields } from './GeneralFields';
import styles from '../../admin.module.css';

interface FormProps {
  categories: AdminCategory[];
  brands: AdminBrand[];
}

/** Create: the general fields plus one or more variant rows (a product always has at least one variant). */
export function NewProductForm({ categories, brands }: FormProps) {
  const [state, action] = useActionState(createProductAction, INITIAL_FORM_STATE);
  const [rows, setRows] = useState([0]);
  return (
    <form action={action} className={styles.grid2}>
      <div className={styles.wide}>
        <FormBanner state={state} />
      </div>
      <GeneralFields state={state} categories={categories} brands={brands} />

      <fieldset className={`${styles.panel} ${styles.wide}`}>
        <legend className="ui-heading-4">{adminMessages.products.variants}</legend>
        {rows.map((row, index) => (
          <div key={row} className={styles.variantCard}>
            <strong>{adminMessages.products.variantRow(formatNumber(index + 1))}</strong>
            <div className={styles.grid2}>
              <Field name={`variants.${index}.sku`} label={adminMessages.products.sku} state={state} ltr required />
              <Field name={`variants.${index}.price`} label={adminMessages.products.price} state={state} ltr inputMode="numeric" required />
              <Field name={`variants.${index}.compareAtPrice`} label={adminMessages.products.compareAt} state={state} ltr inputMode="numeric" />
              <Field name={`variants.${index}.stock`} label={adminMessages.products.stock} state={state} ltr inputMode="numeric" defaultValue={0} required />
            </div>
          </div>
        ))}
        <div>
          <Button type="button" variant="secondary" size="sm" onClick={() => setRows((r) => [...r, (r.at(-1) ?? 0) + 1])}>
            {adminMessages.products.addVariant}
          </Button>
        </div>
      </fieldset>

      <div className={styles.row}>
        <SubmitButton>{adminMessages.create}</SubmitButton>
      </div>
    </form>
  );
}

export function EditProductForm({ product, categories, brands }: FormProps & { product: AdminProduct }) {
  const [state, action] = useActionState(updateProductAction, INITIAL_FORM_STATE);
  return (
    <form action={action} className={styles.grid2}>
      <input type="hidden" name="id" value={product.id} />
      <div className={styles.wide}>
        <FormBanner state={state} />
      </div>
      <div key={product.updatedAtUtc} style={{ display: 'contents' }}>
        <GeneralFields state={state} categories={categories} brands={brands} product={product} />
      </div>
      <div className={styles.row}>
        <SubmitButton>{adminMessages.save}</SubmitButton>
      </div>
    </form>
  );
}
