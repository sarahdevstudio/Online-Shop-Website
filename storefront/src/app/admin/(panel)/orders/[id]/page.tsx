import { notFound } from 'next/navigation';
import { adminGet } from '@/lib/admin/api';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminOrder } from '@/lib/admin/types';
import { formatJalaliDate, formatNumber } from '@/lib/format';
import { messages } from '@/lib/messages';
import { OrderActions } from './OrderActions';
import styles from '../../../admin.module.css';

export default async function AdminOrderPage({ params }: { params: Promise<{ id: string }> }) {
  const id = Number((await params).id);
  if (!Number.isSafeInteger(id) || id <= 0) notFound();
  const data = await adminGet<AdminOrder>(`/admin/orders/${id}`);
  if (!data) notFound();
  const { order } = data;
  const toman = (value: number) => `${formatNumber(value)} ${messages.currencyUnit}`;
  const card = order.paymentInstructions;

  return (
    <>
      <h1 className="ui-heading-2">
        {adminMessages.orders.detail} {formatNumber(order.number)}
      </h1>

      <div className={styles.grid2}>
        <section className={styles.panel} aria-labelledby="status">
          <h2 id="status" className="ui-heading-4">
            {adminMessages.orders.status}
          </h2>
          <p>
            {messages.statuses[order.status]} — {messages.methods[order.paymentMethod]} — {messages.paymentStatuses[order.paymentStatus]}
          </p>
          <p className={styles.muted}>
            {adminMessages.orders.date}: {formatJalaliDate(order.createdAtUtc)}
          </p>
          <p className={`${styles.muted} ${styles.ltr}`}>{data.clientIp}</p>
        </section>

        <section className={styles.panel} aria-labelledby="customer">
          <h2 id="customer" className="ui-heading-4">
            {adminMessages.orders.address}
          </h2>
          <p>
            {order.fullName} — <span className={styles.ltr}>{order.mobile}</span>
          </p>
          <p>
            {order.province}، {order.city}، {order.address}
          </p>
          <p>
            {messages.postalCode}: <span className={styles.ltr}>{order.postalCode}</span>
          </p>
          {order.note && <p className={styles.muted}>{order.note}</p>}
        </section>
      </div>

      {order.paymentMethod === 'cardToCard' && (
        <section className={styles.panel} aria-labelledby="transfer">
          <h2 id="transfer" className="ui-heading-4">
            {adminMessages.orders.transfer}
          </h2>
          {order.transferReference ? (
            <ul>
              <li>
                {messages.transferTracking}: <bdi dir="ltr">{order.transferReference.tracking}</bdi>
              </li>
              <li>
                {messages.transferTime}: {order.transferReference.time}
              </li>
              <li>
                {messages.transferLast4}: <bdi dir="ltr">{order.transferReference.cardLast4}</bdi>
              </li>
              <li>
                {messages.exactAmount}: {toman(order.total)}
              </li>
            </ul>
          ) : (
            <p className={styles.muted}>{adminMessages.orders.noTransfer}</p>
          )}
          {card && (
            <p className={styles.muted}>
              {adminMessages.orders.cardSnapshot}: <bdi dir="ltr">{card.cardNumber}</bdi> — {card.holderName} {card.bankName && `— ${card.bankName}`}
            </p>
          )}
        </section>
      )}

      <section className={styles.panel} aria-labelledby="lines">
        <h2 id="lines" className="ui-heading-4">
          {adminMessages.orders.lines}
        </h2>
        <div className={styles.tableWrap}>
          <table className={styles.table}>
            <tbody>
              {order.lines.map((l) => (
                <tr key={l.sku}>
                  <td>
                    {l.productName}
                    {l.variantLabel ? ` (${l.variantLabel})` : ''} <span className={`${styles.muted} ${styles.ltr}`}>{l.sku}</span>
                  </td>
                  <td className={styles.num}>
                    {formatNumber(l.unitPrice)} × {formatNumber(l.quantity)}
                  </td>
                  <td className={styles.num}>{formatNumber(l.lineTotal)}</td>
                </tr>
              ))}
              <tr>
                <td colSpan={2}>{messages.shipping}</td>
                <td className={styles.num}>{formatNumber(order.shippingFee)}</td>
              </tr>
              <tr>
                <td colSpan={2}>
                  <strong>{messages.total}</strong>
                </td>
                <td className={styles.num}>
                  <strong>{formatNumber(order.total)}</strong>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <OrderActions id={data.id} status={order.status} paymentStatus={order.paymentStatus} paymentMethod={order.paymentMethod} />

      <section className={styles.panel} aria-labelledby="log">
        <h2 id="log" className="ui-heading-4">
          {adminMessages.orders.log}
        </h2>
        <ul>
          {data.statusLog.map((entry, i) => (
            <li key={i}>
              {formatJalaliDate(entry.atUtc)} — {entry.from ? `${messages.statuses[entry.from]} ← ` : ''}
              {messages.statuses[entry.to]} {entry.adminUserId ? '' : `(${adminMessages.orders.byCustomer})`}
            </li>
          ))}
        </ul>
      </section>
    </>
  );
}
