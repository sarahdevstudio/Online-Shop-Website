'use client';

import { useActionState } from 'react';
import { Form, FormActions, FormGrid, FormSection, SelectField, SubmitButton, TextField } from '@/components/form';
import { INITIAL_FORM_STATE } from '@/lib/admin/form-state';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminAttribute, AdminProduct } from '@/lib/admin/types';
import { FormBanner } from '../../_components/FormBits';
import { saveAttributesAction } from './actions';

/**
 * One block per scope (the whole product, then each variant), one field per attribute. Option attributes are selects,
 * text attributes are inputs. Saving replaces the product's whole attribute value set.
 */
export function AttributesEditor({ product, attributes }: { product: AdminProduct; attributes: AdminAttribute[] }) {
  const [state, action] = useActionState(saveAttributesAction, INITIAL_FORM_STATE);
  const valueOf = (attributeId: number, variantId: number | null) =>
    product.attributes.find((a) => a.attributeId === attributeId && a.variantId === variantId);

  const field = (attribute: AdminAttribute, variantId: number | null) => {
    const name = `attr:${attribute.id}:${variantId ?? 'p'}`;
    const label = attribute.unit ? `${attribute.name} (${attribute.unit})` : attribute.name;
    const current = valueOf(attribute.id, variantId);
    if (attribute.type === 'option') {
      return (
        <SelectField key={name} name={name} label={label} defaultValue={current?.optionId ?? ''}>
          <option value="">{adminMessages.none}</option>
          {attribute.options.map((o) => (
            <option key={o.id} value={o.id}>
              {o.value}
            </option>
          ))}
        </SelectField>
      );
    }
    return <TextField key={name} name={name} label={label} defaultValue={current?.text ?? ''} maxLength={200} />;
  };

  const scopes: Array<{ key: string; title: string; variantId: number | null }> = [
    { key: 'p', title: adminMessages.products.productLevel, variantId: null },
    ...product.variants.map((v) => ({ key: String(v.id), title: `${adminMessages.products.variants}: ${v.sku}`, variantId: v.id })),
  ];

  return (
    <div id="attributes">
      <FormSection title={adminMessages.products.attributes}>
        <Form action={action} errors={state.errors} banner={<FormBanner state={state} />}>
          <input type="hidden" name="productId" value={product.id} />
          <div key={product.updatedAtUtc} className="ui-form">
            {scopes.map((scope) => (
              <FormSection key={scope.key} card title={scope.title}>
                <FormGrid>{attributes.map((attribute) => field(attribute, scope.variantId))}</FormGrid>
              </FormSection>
            ))}
          </div>
          <FormActions>
            <SubmitButton>{adminMessages.save}</SubmitButton>
          </FormActions>
        </Form>
      </FormSection>
    </div>
  );
}
