'use client';

import { useActionState } from 'react';
import { INITIAL_FORM_STATE } from '@/lib/admin/form-state';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminCategory } from '@/lib/admin/types';
import { CheckField, Field, FormBanner, SubmitButton } from '../../_components/FormBits';
import { deleteCategoryAction, saveCategoryAction } from './actions';
import styles from '../../admin.module.css';

export function CategoryForm({ category, roots }: { category?: AdminCategory; roots: AdminCategory[] }) {
  const [state, action] = useActionState(saveCategoryAction, INITIAL_FORM_STATE);
  const parents = roots.filter((r) => r.id !== category?.id);
  return (
    <form action={action} className={styles.grid2}>
      {category && <input type="hidden" name="id" value={category.id} />}
      <div className={styles.wide}>
        <FormBanner state={state} />
      </div>
      <Field name="name" label={adminMessages.products.name} state={state} defaultValue={category?.name} required />
      {category ? (
        <p className={`${styles.ltr} ${styles.muted}`}>{category.slug}</p>
      ) : (
        <Field name="slug" label={adminMessages.products.slug} hint={adminMessages.products.slugHint} state={state} ltr required />
      )}
      <label className={styles.field}>
        <span className={styles.fieldLabel}>{adminMessages.taxonomy.parent}</span>
        <select className="ui-control" name="parentId" defaultValue={category?.parentId ?? ''}>
          <option value="">{adminMessages.taxonomy.root}</option>
          {parents.map((p) => (
            <option key={p.id} value={p.id}>
              {p.name}
            </option>
          ))}
        </select>
      </label>
      <Field name="sortOrder" label={adminMessages.products.sortOrder} state={state} type="number" defaultValue={category?.sortOrder ?? 0} />
      <Field name="description" label={adminMessages.products.description} state={state} defaultValue={category?.description} multiline wide />
      <CheckField name="isActive" label={adminMessages.active} defaultChecked={category?.isActive ?? true} />
      <div className={styles.row}>
        <SubmitButton>{category ? adminMessages.save : adminMessages.create}</SubmitButton>
      </div>
    </form>
  );
}

export function DeleteCategoryForm({ id }: { id: number }) {
  const [state, action] = useActionState(deleteCategoryAction, INITIAL_FORM_STATE);
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
