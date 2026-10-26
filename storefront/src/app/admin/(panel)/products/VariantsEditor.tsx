'use client';

import { useActionState } from 'react';
import { INITIAL_FORM_STATE } from '@/lib/admin/form-state';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminVariant, StockHistoryEntry } from '@/lib/admin/types';
import { formatJalaliDate, formatNumber } from '@/lib/format';
import { CheckField, Field, FormBanner, SubmitButton } from '../../_components/FormBits';
import { addVariantAction, adjustStockAction, saveVariantAction } from './actions';
import styles from '../../admin.module.css';

const t = adminMessages.products;

function VariantForm({ productId, variant }: { productId: number; variant: AdminVariant }) {
  const [state, action] = useActionState(saveVariantAction, INITIAL_FORM_STATE);
  return (
    <form action={action} className={styles.grid2}>
      <input type="hidden" name="productId" value={productId} />
      <input type="hidden" name="variantId" value={variant.id} />
      {/* The version this form was loaded with: if anything changed the variant meanwhile, saving is a 409 "data changed". */}
      <input type="hidden" name="rowVersion" value={variant.rowVersion} />
      <div className={styles.wide}>
        <FormBanner state={state} />
      </div>
      {/* Only the fields are keyed by the version: after any save (or a reload) they show the stored values, while the form
          itself - and with it the success / "data changed" banner above - is not remounted. */}
      <div key={variant.rowVersion} style={{ display: 'contents' }}>
        <Field name="sku" label={t.sku} state={state} ltr defaultValue={variant.sku} required />
        <Field name="price" label={t.price} state={state} ltr inputMode="numeric" defaultValue={variant.price} required />
        <Field name="compareAtPrice" label={t.compareAt} state={state} ltr inputMode="numeric" defaultValue={variant.compareAtPrice} />
        <Field name="stock" label={t.setStock} state={state} ltr inputMode="numeric" defaultValue={variant.stock} required />
        <Field name="sortOrder" label={t.sortOrder} state={state} ltr inputMode="numeric" defaultValue={variant.sortOrder} />
        <CheckField name="isActive" label={adminMessages.active} defaultChecked={variant.isActive} />
      </div>
      <div className={styles.row}>
        <SubmitButton>{adminMessages.save}</SubmitButton>
      </div>
    </form>
  );
}

function AdjustForm({ productId, variantId }: { productId: number; variantId: number }) {
  const [state, action] = useActionState(adjustStockAction, INITIAL_FORM_STATE);
  return (
    <form action={action} className={styles.row}>
      <input type="hidden" name="productId" value={productId} />
      <input type="hidden" name="variantId" value={variantId} />
      <Field name="delta" label={t.adjust} hint={t.adjustHint} state={state} ltr inputMode="numeric" />
      <Field name="reason" label={t.reason} state={state} />
      <SubmitButton variant="secondary">{t.apply}</SubmitButton>
      <FormBanner state={state} />
    </form>
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
    <form action={action} className={styles.grid2}>
      <input type="hidden" name="productId" value={productId} />
      <div className={styles.wide}>
        <FormBanner state={state} />
      </div>
      <Field name="sku" label={t.sku} state={state} ltr required />
      <Field name="price" label={t.price} state={state} ltr inputMode="numeric" required />
      <Field name="compareAtPrice" label={t.compareAt} state={state} ltr inputMode="numeric" />
      <Field name="stock" label={t.stock} state={state} ltr inputMode="numeric" defaultValue={0} required />
      <CheckField name="isActive" label={adminMessages.active} defaultChecked />
      <div className={styles.row}>
        <SubmitButton>{t.addVariant}</SubmitButton>
      </div>
    </form>
  );
}

export function VariantsEditor({ productId, variants, history }: { productId: number; variants: AdminVariant[]; history: Record<number, StockHistoryEntry[]> }) {
  return (
    <section className={styles.panel} aria-labelledby="variants">
      <h2 id="variants" className="ui-heading-4">
        {t.variants}
      </h2>
      {variants.map((variant, index) => (
        <div key={variant.id} className={styles.variantCard}>
          <strong>
            {t.variantRow(formatNumber(index + 1))} · <bdi dir="ltr">{variant.sku}</bdi> · {t.stock}: {formatNumber(variant.stock)}
          </strong>
          <VariantForm productId={productId} variant={variant} />
          <AdjustForm productId={productId} variantId={variant.id} />
          <History entries={history[variant.id] ?? []} />
        </div>
      ))}
      <details>
        <summary>{t.addVariant}</summary>
        <AddVariantForm productId={productId} />
      </details>
    </section>
  );
}
