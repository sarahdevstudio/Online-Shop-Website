'use client';

import { adminMessages } from '@/lib/admin/messages';
import type { FormState } from '@/lib/admin/form-state';
import type { AdminBrand, AdminCategory, AdminProduct } from '@/lib/admin/types';
import { Field } from '../../_components/FormBits';
import styles from '../../admin.module.css';

interface Props {
  state: FormState;
  categories: AdminCategory[];
  brands: AdminBrand[];
  product?: AdminProduct;
}

/** Name, slug (create only), category, brand, status, description: shared by the create and edit forms. */
export function GeneralFields({ state, categories, brands, product }: Props) {
  const roots = categories.filter((c) => c.parentId === null);
  return (
    <>
      <Field name="name" label={adminMessages.products.name} state={state} defaultValue={product?.name} required />
      {product ? (
        <p className={`${styles.ltr} ${styles.muted}`}>{product.slug}</p>
      ) : (
        <Field name="slug" label={adminMessages.products.slug} hint={adminMessages.products.slugHint} state={state} ltr required />
      )}
      <label className={styles.field}>
        <span className={styles.fieldLabel}>{adminMessages.products.category}</span>
        <select className="ui-control" name="categoryId" defaultValue={product?.categoryId ?? ''} required>
          <option value="" disabled>
            —
          </option>
          {roots.map((root) => (
            <optgroup key={root.id} label={root.name}>
              <option value={root.id}>{root.name}</option>
              {categories
                .filter((c) => c.parentId === root.id)
                .map((child) => (
                  <option key={child.id} value={child.id}>
                    {root.name} › {child.name}
                  </option>
                ))}
            </optgroup>
          ))}
        </select>
        {state.errors.categoryId && <span className={styles.error}>{state.errors.categoryId}</span>}
      </label>
      <label className={styles.field}>
        <span className={styles.fieldLabel}>{adminMessages.products.brand}</span>
        <select className="ui-control" name="brandId" defaultValue={product?.brandId ?? ''}>
          <option value="">{adminMessages.products.noBrand}</option>
          {brands.map((b) => (
            <option key={b.id} value={b.id}>
              {b.name}
            </option>
          ))}
        </select>
      </label>
      <label className={styles.field}>
        <span className={styles.fieldLabel}>{adminMessages.products.status}</span>
        <select className="ui-control" name="status" defaultValue={product?.status ?? 'draft'}>
          {Object.entries(adminMessages.products.statuses).map(([value, label]) => (
            <option key={value} value={value}>
              {label}
            </option>
          ))}
        </select>
        <span className={styles.hint}>{adminMessages.products.archiveHint}</span>
      </label>
      <Field name="description" label={adminMessages.products.description} state={state} defaultValue={product?.description} multiline wide />
    </>
  );
}
