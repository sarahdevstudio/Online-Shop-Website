'use client';

import { useActionState } from 'react';
import { INITIAL_FORM_STATE } from '@/lib/admin/form-state';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminAttribute, AdminProduct } from '@/lib/admin/types';
import { FormBanner, SubmitButton } from '../../_components/FormBits';
import { saveAttributesAction } from './actions';
import styles from '../../admin.module.css';

/**
 * A grid: one row per attribute, one column for the whole product and one per variant. Option attributes are selects,
 * text attributes are inputs. Saving replaces the product's whole attribute value set.
 */
export function AttributesEditor({ product, attributes }: { product: AdminProduct; attributes: AdminAttribute[] }) {
  const [state, action] = useActionState(saveAttributesAction, INITIAL_FORM_STATE);
  const valueOf = (attributeId: number, variantId: number | null) =>
    product.attributes.find((a) => a.attributeId === attributeId && a.variantId === variantId);

  const cell = (attribute: AdminAttribute, variantId: number | null) => {
    const name = `attr:${attribute.id}:${variantId ?? 'p'}`;
    const current = valueOf(attribute.id, variantId);
    if (attribute.type === 'option') {
      return (
        <select className="ui-control" name={name} defaultValue={current?.optionId ?? ''} aria-label={attribute.name}>
          <option value="">{adminMessages.none}</option>
          {attribute.options.map((o) => (
            <option key={o.id} value={o.id}>
              {o.value}
            </option>
          ))}
        </select>
      );
    }
    return <input className="ui-control" name={name} defaultValue={current?.text ?? ''} aria-label={attribute.name} maxLength={200} />;
  };

  return (
    <section className={styles.panel} aria-labelledby="attributes">
      <h2 id="attributes" className="ui-heading-4">
        {adminMessages.products.attributes}
      </h2>
      <form action={action} className={styles.grid2}>
        <input type="hidden" name="productId" value={product.id} />
        <div className={styles.wide}>
          <FormBanner state={state} />
        </div>
        <div key={product.updatedAtUtc} className={`${styles.tableWrap} ${styles.wide}`}>
          <table className={styles.table}>
            <thead>
              <tr>
                <th />
                <th>{adminMessages.products.productLevel}</th>
                {product.variants.map((v) => (
                  <th key={v.id} className={styles.ltr}>
                    {v.sku}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {attributes.map((attribute) => (
                <tr key={attribute.id}>
                  <th scope="row">
                    {attribute.name}
                    {attribute.unit ? ` (${attribute.unit})` : ''}
                  </th>
                  <td>{cell(attribute, null)}</td>
                  {product.variants.map((v) => (
                    <td key={v.id}>{cell(attribute, v.id)}</td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className={styles.row}>
          <SubmitButton>{adminMessages.save}</SubmitButton>
        </div>
      </form>
    </section>
  );
}
