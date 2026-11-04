'use client';

import { useActionState } from 'react';
import { CheckField, Form, FormActions, FormBlock, FormGrid, FormSection, RequiredNote, SelectField, SubmitButton, TextAreaField, TextField } from '@/components/form';
import { INITIAL_FORM_STATE } from '@/lib/admin/form-state';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminCategory, RouteTarget } from '@/lib/admin/types';
import { PathField } from '../../_components/PathField';
import { FormBanner } from '../../_components/FormBits';
import { deleteCategoryAction, saveCategoryAction } from './actions';

/** Create (no `category`, with a `title`) or edit a category. */
export function CategoryForm({ category, roots, title, route }: { category?: AdminCategory; roots: AdminCategory[]; title?: string; route?: RouteTarget }) {
  const [state, action] = useActionState(saveCategoryAction, INITIAL_FORM_STATE);
  const parents = roots.filter((r) => r.id !== category?.id);
  return (
    <Form action={action} errors={state.errors} banner={<FormBanner state={state} />}>
      {category && <input type="hidden" name="id" value={category.id} />}
      <FormSection card={Boolean(title)} title={title}>
        <RequiredNote />
        <FormGrid>
          <TextField name="name" label={adminMessages.products.name} defaultValue={category?.name} required />
          {category ? (
            <FormBlock title={adminMessages.products.slug} full={false}>
              <bdi dir="ltr">{category.slug}</bdi>
            </FormBlock>
          ) : (
            <TextField name="slug" label={adminMessages.products.slug} hint={adminMessages.products.slugHint} ltr required />
          )}
          <SelectField name="parentId" label={adminMessages.taxonomy.parent} defaultValue={category?.parentId ?? ''}>
            <option value="">{adminMessages.taxonomy.root}</option>
            {parents.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </SelectField>
          <TextField name="sortOrder" label={adminMessages.products.sortOrder} defaultValue={category?.sortOrder ?? 0} inputMode="numeric" ltr autoComplete="off" />
          <TextAreaField name="description" label={adminMessages.products.description} defaultValue={category?.description} rows={3} />
          <PathField key={route?.rowVersion ?? 'new'} route={route} defaultPattern="/category/<slug>" />
          <CheckField name="isActive" label={adminMessages.active} defaultChecked={category?.isActive ?? true} full />
        </FormGrid>
        <FormActions>
          <SubmitButton>{category ? adminMessages.save : adminMessages.create}</SubmitButton>
        </FormActions>
      </FormSection>
    </Form>
  );
}

export function DeleteCategoryForm({ id }: { id: number }) {
  const [state, action] = useActionState(deleteCategoryAction, INITIAL_FORM_STATE);
  return (
    <Form action={action} errors={state.errors} banner={<FormBanner state={state} />}>
      <input type="hidden" name="id" value={id} />
      <FormActions>
        <SubmitButton variant="danger">{adminMessages.delete}</SubmitButton>
      </FormActions>
    </Form>
  );
}
