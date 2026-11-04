'use client';

import { useActionState, useState } from 'react';
import { Button } from '@/components/Button';
import { Form, FormActions, FormGrid, FormSection, RequiredNote, SubmitButton, TextField } from '@/components/form';
import { INITIAL_FORM_STATE } from '@/lib/admin/form-state';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminBrand, AdminCategory, AdminProduct, RouteTarget } from '@/lib/admin/types';
import { formatNumber } from '@/lib/format';
import { FormBanner } from '../../_components/FormBits';
import { createProductAction, updateProductAction } from './actions';
import { GeneralFields } from './GeneralFields';

interface FormProps {
  categories: AdminCategory[];
  brands: AdminBrand[];
}

/** Create: the general fields plus one or more variant rows (a product always has at least one variant). */
export function NewProductForm({ categories, brands }: FormProps) {
  const [state, action] = useActionState(createProductAction, INITIAL_FORM_STATE);
  const [rows, setRows] = useState([0]);
  const t = adminMessages.products;
  return (
    <Form action={action} errors={state.errors} banner={<FormBanner state={state} />}>
      <RequiredNote />
      <FormSection card>
        <FormGrid>
          <GeneralFields categories={categories} brands={brands} />
        </FormGrid>
      </FormSection>

      <FormSection card title={t.variants}>
        {rows.map((row, index) => (
          <FormSection key={row} title={t.variantRow(formatNumber(index + 1))}>
            <FormGrid>
              <TextField name={`variants.${index}.sku`} label={t.sku} ltr required />
              <TextField name={`variants.${index}.price`} label={t.price} ltr inputMode="numeric" autoComplete="off" required />
              <TextField name={`variants.${index}.compareAtPrice`} label={t.compareAt} ltr inputMode="numeric" autoComplete="off" />
              <TextField name={`variants.${index}.stock`} label={t.stock} ltr inputMode="numeric" autoComplete="off" defaultValue={0} required />
            </FormGrid>
          </FormSection>
        ))}
        <FormActions>
          <Button type="button" variant="secondary" onClick={() => setRows((r) => [...r, (r.at(-1) ?? 0) + 1])}>
            {t.addVariant}
          </Button>
        </FormActions>
      </FormSection>

      <FormActions>
        <SubmitButton>{adminMessages.create}</SubmitButton>
      </FormActions>
    </Form>
  );
}

export function EditProductForm({ product, categories, brands, route }: FormProps & { product: AdminProduct; route: RouteTarget }) {
  const [state, action] = useActionState(updateProductAction, INITIAL_FORM_STATE);
  return (
    <Form action={action} errors={state.errors} banner={<FormBanner state={state} />}>
      <input type="hidden" name="id" value={product.id} />
      <RequiredNote />
      <div key={product.updatedAtUtc}>
        <FormGrid>
          <GeneralFields categories={categories} brands={brands} product={product} route={route} />
        </FormGrid>
      </div>
      <FormActions>
        <SubmitButton>{adminMessages.save}</SubmitButton>
      </FormActions>
    </Form>
  );
}
