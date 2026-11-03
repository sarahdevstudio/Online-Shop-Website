'use client';

import { useActionState } from 'react';
import { CheckField, Form, FormActions, FormGrid, FormSection, RequiredNote, SubmitButton, TextField } from '@/components/form';
import { INITIAL_FORM_STATE } from '@/lib/admin/form-state';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminVariant, StockHistoryEntry } from '@/lib/admin/types';
import { formatJalaliDate, formatNumber } from '@/lib/format';
import { FormBanner } from '../../_components/FormBits';
import { addVariantAction, adjustStockAction, saveVariantAction } from './actions';
import styles from '../../admin.module.css';

const t = adminMessages.products;

function VariantForm({ productId, variant }: { productId: number; variant: AdminVariant }) {
  const [state, action] = useActionState(saveVariantAction, INITIAL_FORM_STATE);
  return (
    <Form action={action} errors={state.errors} banner={<FormBanner state={state} />}>
      <input type="hidden" name="productId" value={productId} />
      <input type="hidden" name="variantId" value={variant.id} />
      {/* The version this form was loaded with: if anything changed the variant meanwhile, saving is a 409 "data changed". */}
      <input type="hidden" name="rowVersion" value={variant.rowVersion} />
      {/* Only the fields are keyed by the version: after any save (or a reload) they show the stored values, while the form
          itself - and with it the success / "data changed" banner above - is not remounted. */}
      <div key={variant.rowVersion}>
        <FormGrid columns={3}>
          <TextField name="sku" label={t.sku} ltr defaultValue={variant.sku} required />
          <TextField name="price" label={t.price} ltr inputMode="numeric" autoComplete="off" defaultValue={variant.price} required />
          <TextField name="compareAtPrice" label={t.compareAt} ltr inputMode="numeric" autoComplete="off" defaultValue={variant.compareAtPrice} />
          <TextField name="stock" label={t.setStock} ltr inputMode="numeric" autoComplete="off" defaultValue={variant.stock} required />
          <TextField name="sortOrder" label={t.sortOrder} ltr inputMode="numeric" autoComplete="off" defaultValue={variant.sortOrder} />
          <CheckField name="isActive" label={adminMessages.active} defaultChecked={variant.isActive} full />
        </FormGrid>
      </div>
      <FormActions>
        <SubmitButton>{adminMessages.save}</SubmitButton>
      </FormActions>
    </Form>
  );
}

function AdjustForm({ productId, variantId }: { productId: number; variantId: number }) {
  const [state, action] = useActionState(adjustStockAction, INITIAL_FORM_STATE);
  return (
    <FormSection hint={t.adjustHint}>
      <Form action={action} errors={state.errors} banner={<FormBanner state={state} />} inline>
        <input type="hidden" name="productId" value={productId} />
        <input type="hidden" name="variantId" value={variantId} />
        <TextField name="delta" label={t.adjust} ltr inputMode="text" autoComplete="off" compact />
        <TextField name="reason" label={t.reason} autoComplete="off" />
        <SubmitButton variant="secondary">{t.apply}</SubmitButton>
      </Form>
    </FormSection>
  );
}

function History({ entries }: { entries: StockHistoryEntry[] }) {
  return (
    <details>
      <summary>{t.history}</summary>
      {entries.length === 0 ? (
        <p className={styles.muted}>{t.noHistory}</p>
      ) : (
        <ul>
          {entries.map((e) => (
            <li key={e.id}>
              {formatJalaliDate(e.atUtc)} — <bdi dir="ltr">{e.delta > 0 ? `+${e.delta}` : e.delta}</bdi> → <bdi dir="ltr">{formatNumber(e.stockAfter)}</bdi> —{' '}
              <bdi dir="ltr">{e.adminUsername ?? adminMessages.none}</bdi> {e.reason && `— ${e.reason}`}
            </li>
          ))}
        </ul>
      )}
    </details>
  );
}

function AddVariantForm({ productId }: { productId: number }) {
  const [state, action] = useActionState(addVariantAction, INITIAL_FORM_STATE);
  return (
    <Form action={action} errors={state.errors} banner={<FormBanner state={state} />}>
      <input type="hidden" name="productId" value={productId} />
      <RequiredNote />
      <FormGrid columns={3}>
        <TextField name="sku" label={t.sku} ltr required />
        <TextField name="price" label={t.price} ltr inputMode="numeric" autoComplete="off" required />
        <TextField name="compareAtPrice" label={t.compareAt} ltr inputMode="numeric" autoComplete="off" />
        <TextField name="stock" label={t.stock} ltr inputMode="numeric" autoComplete="off" defaultValue={0} required />
        <CheckField name="isActive" label={adminMessages.active} defaultChecked full />
      </FormGrid>
      <FormActions>
        <SubmitButton>{t.addVariant}</SubmitButton>
      </FormActions>
    </Form>
  );
}

export function VariantsEditor({ productId, variants, history }: { productId: number; variants: AdminVariant[]; history: Record<number, StockHistoryEntry[]> }) {
  return (
    <div id="variants">
      <FormSection title={t.variants}>
        {variants.map((variant, index) => (
          <FormSection key={variant.id} card title={`${t.variantRow(formatNumber(index + 1))} · ${variant.sku} · ${t.stock}: ${formatNumber(variant.stock)}`}>
            <VariantForm productId={productId} variant={variant} />
            <AdjustForm productId={productId} variantId={variant.id} />
            <History entries={history[variant.id] ?? []} />
          </FormSection>
        ))}
        <FormSection card>
          <details>
            <summary>{t.addVariant}</summary>
            <AddVariantForm productId={productId} />
          </details>
        </FormSection>
      </FormSection>
    </div>
  );
}
