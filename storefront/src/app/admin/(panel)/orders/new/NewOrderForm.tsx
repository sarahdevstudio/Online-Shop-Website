'use client';

import { useState, useTransition } from 'react';
import Link from 'next/link';
import { Button } from '@/components/Button';
import { ComboField, Form, FormActions, FormGrid, SelectField, TextAreaField, TextField } from '@/components/form';
import { adminMessages } from '@/lib/admin/messages';
import { formatNumber } from '@/lib/format';
import type { CartDto } from '@/lib/store-types';
import {
  addDraftItemAction,
  applyDraftDiscountAction,
  placeDraftOrderAction,
  removeDraftDiscountAction,
  searchVariantsAction,
  setDraftQuantityAction,
} from './actions';

const t = adminMessages.orders.newOrder;

export function NewOrderForm({ initial }: { initial: CartDto }) {
  const [cart, setCart] = useState(initial);
  const [pending, startTransition] = useTransition();
  const [error, setError] = useState<string | null>(null);
  const [discountCode, setDiscountCode] = useState('');
  const [placed, setPlaced] = useState<{ number: number; accessToken: string } | null>(null);
  const [copied, setCopied] = useState(false);

  const [fullName, setFullName] = useState('');
  const [mobile, setMobile] = useState('');
  const [province, setProvince] = useState('');
  const [city, setCity] = useState('');
  const [address, setAddress] = useState('');
  const [postalCode, setPostalCode] = useState('');
  const [note, setNote] = useState('');
  const [paymentMethod, setPaymentMethod] = useState<'cardToCard' | 'cashOnDelivery'>(cart.paymentMethods.cardToCard ? 'cardToCard' : 'cashOnDelivery');

  function applyOutcome(outcome: { ok: true; value: CartDto } | { ok: false; message: string }) {
    if (outcome.ok) {
      setCart(outcome.value);
      setError(null);
    } else {
      setError(outcome.message);
    }
  }

  function addVariant(variantId: number) {
    startTransition(async () => applyOutcome(await addDraftItemAction(variantId, 1)));
  }

  function setQuantity(variantId: number, quantity: number) {
    startTransition(async () => applyOutcome(await setDraftQuantityAction(variantId, quantity)));
  }

  function applyDiscount() {
    if (!discountCode.trim()) return;
    startTransition(async () => applyOutcome(await applyDraftDiscountAction(discountCode.trim())));
  }

  function removeDiscount() {
    startTransition(async () => applyOutcome(await removeDraftDiscountAction()));
  }

  function place() {
    startTransition(async () => {
      const outcome = await placeDraftOrderAction({ fullName, mobile, province, city, address, postalCode, note, paymentMethod, expectedTotal: cart.total });
      if (outcome.ok) {
        setPlaced({ number: outcome.value.number, accessToken: outcome.value.accessToken });
        setError(null);
      } else {
        setError(outcome.message);
      }
    });
  }

  if (placed) {
    const link = typeof window === 'undefined' ? '' : `${window.location.origin}/order/${placed.accessToken}`;
    return (
      <div className="ui-panel">
        <h2 className="ui-heading-3">{t.placedTitle(formatNumber(placed.number))}</h2>
        <p>{t.trackingLink}</p>
        <p>
          <bdi dir="ltr">
            <code>{link}</code>
          </bdi>
        </p>
        <FormActions>
          <button
            type="button"
            className="ui-control"
            onClick={() => {
              navigator.clipboard.writeText(link).then(() => {
                setCopied(true);
                setTimeout(() => setCopied(false), 2000);
              });
            }}
          >
            {copied ? t.copied : t.copyLink}
          </button>
          <Link href="/admin/orders/new" className="ui-control">
            {t.another}
          </Link>
        </FormActions>
      </div>
    );
  }

  return (
    <Form banner={error ? <p className="ui-error" role="alert">{error}</p> : undefined}>
      <section className="ui-panel">
        <h2 className="ui-heading-3">{t.lines}</h2>
        <ComboField
          name="variantSearch"
          label={t.searchLabel}
          placeholder={t.searchPlaceholder}
          selectedLabel=""
          search={searchVariantsAction}
          onSelect={(option) => addVariant(Number(option.value))}
          messages={{ searching: t.searching, noResults: t.noResults, results: t.results }}
        />
        {cart.lines.length === 0 ? (
          <p className="ui-hint">{t.noLines}</p>
        ) : (
          <table className="ui-table">
            <thead>
              <tr>
                <th>{t.product}</th>
                <th>{t.quantity}</th>
                <th>{adminMessages.orders.total}</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {cart.lines.map((line) => (
                <tr key={line.variantId}>
                  <td>
                    {line.productName}
                    {line.variantLabel ? ` — ${line.variantLabel}` : ''} <bdi dir="ltr">({line.sku})</bdi>
                  </td>
                  <td>
                    <input
                      type="number"
                      min={0}
                      max={line.maxQuantity}
                      value={line.quantity}
                      className="ui-control"
                      style={{ inlineSize: '5rem' }}
                      onChange={(e) => setQuantity(line.variantId, Math.max(0, Number(e.target.value) || 0))}
                      disabled={pending}
                    />
                  </td>
                  <td>{formatNumber(line.lineTotal)}</td>
                  <td>
                    <button type="button" className="ui-control" onClick={() => setQuantity(line.variantId, 0)} disabled={pending}>
                      {t.remove}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}

        <FormGrid columns="pair">
          <TextField name="discountCode" label={t.discountCode} value={discountCode} onChange={(e) => setDiscountCode(e.target.value)} ltr />
          <FormActions>
            {cart.discount ? (
              <button type="button" className="ui-control" onClick={removeDiscount} disabled={pending}>
                {t.removeDiscount}
              </button>
            ) : (
              <button type="button" className="ui-control" onClick={applyDiscount} disabled={pending || !discountCode.trim()}>
                {t.applyDiscount}
              </button>
            )}
          </FormActions>
        </FormGrid>

        <p>
          {t.subtotal}: {formatNumber(cart.subtotal)} — {t.shippingFee}: {formatNumber(cart.shippingFee)} — <strong>{t.total}: {formatNumber(cart.total)}</strong>
        </p>
      </section>

      <section className="ui-panel">
        <FormGrid columns="pair">
          <TextField name="fullName" label={t.fullName} required value={fullName} onChange={(e) => setFullName(e.target.value)} />
          <TextField name="mobile" label={t.mobile} required ltr value={mobile} onChange={(e) => setMobile(e.target.value)} />
          <TextField name="province" label={t.province} required value={province} onChange={(e) => setProvince(e.target.value)} />
          <TextField name="city" label={t.city} required value={city} onChange={(e) => setCity(e.target.value)} />
        </FormGrid>
        <TextAreaField name="address" label={t.address} required value={address} onChange={(e) => setAddress(e.target.value)} />
        <FormGrid columns="pair">
          <TextField name="postalCode" label={t.postalCode} required ltr value={postalCode} onChange={(e) => setPostalCode(e.target.value)} />
          <SelectField name="paymentMethod" label={t.paymentMethod} value={paymentMethod} onChange={(e) => setPaymentMethod(e.target.value as 'cardToCard' | 'cashOnDelivery')}>
            {cart.paymentMethods.cardToCard && <option value="cardToCard">{t.cardToCard}</option>}
            {cart.paymentMethods.cashOnDelivery && <option value="cashOnDelivery">{t.cashOnDelivery}</option>}
          </SelectField>
        </FormGrid>
        <TextAreaField name="note" label={t.note} value={note} onChange={(e) => setNote(e.target.value)} />

        <FormActions>
          <Button type="button" loading={pending} onClick={place} disabled={pending || cart.lines.length === 0 || !fullName || !mobile || !province || !city || !address || !postalCode}>
            {pending ? t.placing : t.place}
          </Button>
        </FormActions>
      </section>
    </Form>
  );
}
