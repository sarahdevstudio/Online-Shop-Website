'use client';

import { SelectField, TextAreaField, TextField, FormBlock } from '@/components/form';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminBrand, AdminCategory, AdminProduct } from '@/lib/admin/types';

interface Props {
  categories: AdminCategory[];
  brands: AdminBrand[];
  product?: AdminProduct;
}

/** Name, slug (create only), category, brand, status, description: shared by the create and edit forms. Rendered inside a <FormGrid>. */
export function GeneralFields({ categories, brands, product }: Props) {
  const roots = categories.filter((c) => c.parentId === null);
  const t = adminMessages.products;
  return (
    <>
      <TextField name="name" label={t.name} defaultValue={product?.name} required />
      {product ? (
        <FormBlock title={t.slug} full={false}>
          <bdi dir="ltr">{product.slug}</bdi>
        </FormBlock>
      ) : (
        <TextField name="slug" label={t.slug} hint={t.slugHint} ltr required />
      )}
      <SelectField name="categoryId" label={t.category} defaultValue={product?.categoryId ?? ''} required>
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
      </SelectField>
      <SelectField name="brandId" label={t.brand} defaultValue={product?.brandId ?? ''}>
        <option value="">{t.noBrand}</option>
        {brands.map((b) => (
          <option key={b.id} value={b.id}>
            {b.name}
          </option>
        ))}
      </SelectField>
      <SelectField name="status" label={t.status} hint={t.archiveHint} defaultValue={product?.status ?? 'draft'}>
        {Object.entries(t.statuses).map(([value, label]) => (
          <option key={value} value={value}>
            {label}
          </option>
        ))}
      </SelectField>
      <TextAreaField name="description" label={t.description} defaultValue={product?.description} />
    </>
  );
}
