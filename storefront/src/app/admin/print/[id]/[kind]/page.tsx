import type { Metadata } from 'next';
import Link from 'next/link';
import { notFound } from 'next/navigation';
import { adminGet } from '@/lib/admin/api';
import { printMessages as p } from '@/lib/admin/print-messages';
import type { AdminOrder } from '@/lib/admin/types';
import { formatDigits, formatJalaliDate, formatNumber } from '@/lib/format';
import { messages } from '@/lib/messages';
import { requestNonce } from '@/lib/nonce';
import { seoDefaults } from '@/lib/site-config';
import { getStoreInfo } from '@/lib/store-info';
import { PrintButton } from './PrintButton';
import styles from './print.module.css';

type Kind = 'invoice' | 'packing-slip';
const KINDS: readonly Kind[] = ['invoice', 'packing-slip'];

export async function generateMetadata({ params }: { params: Promise<{ kind: string }> }): Promise<Metadata> {
  const { kind } = await params;
  return { title: kind === 'packing-slip' ? p.packingTitle : p.invoiceTitle, robots: { index: false, follow: false } };
}

/**
 * The printable invoice and packing slip of one order (A4 or A5, Persian, RTL). Same permission as the order screen: the API
 * answers 403 for an account without Orders.Manage. No prices on the packing slip. The paper size is an @page rule, so it is
 * injected as a nonce-tagged style (the CSP allows no other inline style element).
 */
export default async function PrintPage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string; kind: string }>;
  searchParams: Promise<{ size?: string }>;
}) {
  const { id: rawId, kind: rawKind } = await params;
  const id = Number(rawId);
  const kind = KINDS.find((k) => k === rawKind);
  if (!kind || !Number.isSafeInteger(id) || id <= 0) notFound();
  const size = (await searchParams).size === 'a5' ? 'a5' : 'a4';

  const [data, store, seo, nonce] = await Promise.all([adminGet<AdminOrder>(`/admin/orders/${id}`), getStoreInfo(), seoDefaults(), requestNonce()]);
  if (!data) notFound();
  const { order } = data;
  const slip = kind === 'packing-slip';
  const base = `/admin/print/${id}/${kind}`;
  const units = order.lines.reduce((sum, line) => sum + line.quantity, 0);
  const address = [order.province, order.city, order.address].join(messages.listSeparator);

  return (
    <main className={styles.desk}>
      <style nonce={nonce} dangerouslySetInnerHTML={{ __html: `@page { size: ${size === 'a5' ? 'A5' : 'A4'}; margin: ${size === 'a5' ? '8mm' : '12mm'}; }` }} />
      <div className={styles.controls}>
        <PrintButton />
        <Link href={`${base}?size=a4`} aria-current={size === 'a4' ? 'true' : undefined}>
          {p.sizeA4}
        </Link>
        <Link href={`${base}?size=a5`} aria-current={size === 'a5' ? 'true' : undefined}>
          {p.sizeA5}
        </Link>
        <Link href={`/admin/orders/${id}`}>{p.back}</Link>
        <Link href={`/admin/print/${id}/${slip ? 'invoice' : 'packing-slip'}?size=${size}`}>{slip ? p.openInvoice : p.openSlip}</Link>
        <p className={styles.hint}>{p.hint}</p>
      </div>

      <article className={`${styles.sheet} ${styles[size]}`}>
        <header className={styles.head}>
          <div>
            <h1>{slip ? p.packingTitle : p.invoiceTitle}</h1>
            <ul className={styles.meta}>
              <li>
                {p.orderNumber}: <strong>{formatDigits(order.number)}</strong>
              </li>
              <li>
                {p.date}: <strong>{formatJalaliDate(order.createdAtUtc)}</strong>
              </li>
            </ul>
          </div>
          <p className={styles.seller}>
            <strong>{seo.siteName}</strong>
            {store?.storePhone && (
              <>
                <br />
                {p.phone}: <span className={styles.ltr}>{store.storePhone}</span>
              </>
            )}
            {store?.storeAddress && (
              <>
                <br />
                {store.storeAddress}
              </>
            )}
          </p>
        </header>

        <section className={styles.box} aria-labelledby="recipient">
          <h2 id="recipient">{p.recipient}</h2>
          <p className={slip ? styles.recipientLarge : undefined}>
            <strong>{order.fullName}</strong> — <span className={styles.ltr}>{order.mobile}</span>
          </p>
          <p className={slip ? styles.recipientLarge : undefined}>{address}</p>
          <p>
            {p.postalCode}: <span className={styles.ltr}>{order.postalCode}</span>
          </p>
          {order.note && (
            <p>
              {p.note}: {order.note}
            </p>
          )}
        </section>

        <table className={styles.table}>
          <thead>
            <tr>
              <th scope="col">{p.row}</th>
              <th scope="col">{p.item}</th>
              <th scope="col">{p.sku}</th>
              <th scope="col" className={styles.num}>
                {p.quantity}
              </th>
              {slip ? (
                <th scope="col">{p.packed}</th>
              ) : (
                <>
                  <th scope="col" className={styles.num}>
                    {p.unitPrice}
                  </th>
                  <th scope="col" className={styles.num}>
                    {p.lineTotal}
                  </th>
                </>
              )}
            </tr>
          </thead>
          <tbody>
            {order.lines.map((line, index) => (
              <tr key={line.sku}>
                <td className={styles.num}>{formatNumber(index + 1)}</td>
                <td>
                  {line.productName}
                  {line.variantLabel ? ` (${line.variantLabel})` : ''}
                </td>
                <td className={styles.ltr}>{line.sku}</td>
                <td className={styles.num}>{formatNumber(line.quantity)}</td>
                {slip ? (
                  <td>
                    <span className={styles.tick} aria-hidden="true" />
                  </td>
                ) : (
                  <>
                    <td className={styles.num}>{formatNumber(line.unitPrice)}</td>
                    <td className={styles.num}>{formatNumber(line.lineTotal)}</td>
                  </>
                )}
              </tr>
            ))}
          </tbody>
        </table>

        {slip ? (
          <p>
            {p.itemsCount}: <strong>{formatNumber(units)}</strong>
          </p>
        ) : (
          <>
            <div className={styles.totals}>
              <div>
                <span>{messages.subtotal}</span>
                <span className={styles.num}>{formatNumber(order.subtotal)}</span>
              </div>
              <div>
                <span>{messages.shipping}</span>
                <span className={styles.num}>{order.shippingFee === 0 ? messages.shippingFree : formatNumber(order.shippingFee)}</span>
              </div>
              <div className={styles.grand}>
                <span>{messages.total}</span>
                <span className={styles.num}>{formatNumber(order.total)}</span>
              </div>
            </div>
            <p>
              {p.payment}: {messages.methods[order.paymentMethod]} — {p.paymentStatus}: {messages.paymentStatuses[order.paymentStatus]}
            </p>
            <p>{p.currency}</p>
            <p className={styles.foot}>{p.thanks}</p>
          </>
        )}
      </article>
    </main>
  );
}
