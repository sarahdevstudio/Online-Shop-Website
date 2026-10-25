import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { Alert } from '@/components/Alert';
import { formatJalaliDate, formatNumber } from '@/lib/format';
import { messages } from '@/lib/messages';
import { getOrder } from '@/lib/store';
import { TransferReferenceForm } from './TransferReferenceForm';
import styles from './order.module.css';

// The token is the credential. No canonical / Open Graph URL (they would repeat it), never indexed, never cached, and
// the Referrer-Policy header (next.config.ts) keeps it from leaking to other sites.
export const metadata: Metadata = {
  title: 'سفارش شما',
  robots: { index: false, follow: false },
  referrer: 'no-referrer',
};

const groupCard = (number: string) => number.replace(/\D/g, '').replace(/(\d{4})(?=\d)/g, '$1 ');

export default async function OrderPage({ params }: { params: Promise<{ token: string }> }) {
  const { token } = await params;
  const order = await getOrder(token);
  if (!order) notFound();

  const pay = order.paymentInstructions;
  const toman = (value: number) => `${formatNumber(value)} ${messages.currencyUnit}`;

  return (
    <div className="ui-container ui-section ui-stack">
      <h1 className="ui-heading-2">{messages.orderPlacedTitle}</h1>
      <Alert variant="success">
        <p>
          {messages.orderNumber}: <strong>{formatNumber(order.number)}</strong>
        </p>
        <p>{messages.orderPlacedText}</p>
      </Alert>

      <div className={styles.layout}>
        <div className="ui-stack">
          {pay && (
            <section className={`${styles.card} ${styles.pay}`} aria-labelledby="pay">
              <h2 id="pay" className="ui-heading-4">
                {messages.payByCardTitle}
              </h2>
              <p>{messages.payByCardText(formatNumber(pay.amount))}</p>
              <dl>
                <div>
                  <dt>{messages.cardNumber}</dt>
                  <dd className={styles.cardNumber}>{groupCard(pay.cardNumber)}</dd>
                </div>
                <div>
                  <dt>{messages.cardHolder}</dt>
                  <dd>{pay.holderName}</dd>
                </div>
                <div>
                  <dt>{messages.exactAmount}</dt>
                  <dd>{toman(pay.amount)}</dd>
                </div>
              </dl>
            </section>
          )}

          {order.status === 'awaitingPayment' && order.paymentMethod === 'cardToCard' && (
            <section className={styles.card} aria-labelledby="transfer">
              <h2 id="transfer" className="ui-heading-4">
                {messages.transferTitle}
              </h2>
              {order.transferReference && (
                <Alert variant="info" heading={messages.transferRegistered}>
                  <p>
                    {messages.transferTracking}: {order.transferReference.tracking}
                  </p>
                  <p>
                    {messages.transferTime}: {order.transferReference.time}
                  </p>
                  <p>
                    {messages.transferLast4}: {order.transferReference.cardLast4}
                  </p>
                  <p>{messages.transferResubmitHint}</p>
                </Alert>
              )}
              <TransferReferenceForm token={token} />
            </section>
          )}
        </div>

        <div className="ui-stack">
          <section className={styles.card} aria-labelledby="status">
            <h2 id="status" className="ui-heading-4">
              {messages.orderStatus}
            </h2>
            <dl>
              <div>
                <dt>{messages.orderStatus}</dt>
                <dd>{messages.statuses[order.status] ?? order.status}</dd>
              </div>
              <div>
                <dt>{messages.paymentStatus}</dt>
                <dd>
                  {messages.methods[order.paymentMethod]} — {messages.paymentStatuses[order.paymentStatus]}
                </dd>
              </div>
              <div>
                <dt>{messages.orderDate}</dt>
                <dd>{formatJalaliDate(order.createdAtUtc)}</dd>
              </div>
            </dl>
          </section>

          <section className={styles.card} aria-labelledby="items">
            <h2 id="items" className="ui-heading-4">
              {messages.orderItems}
            </h2>
            <ul className={styles.lines}>
              {order.lines.map((line) => (
                <li key={line.sku} className={styles.row}>
                  <span>
                    {line.productName}
                    {line.variantLabel ? ` (${line.variantLabel})` : ''} × {formatNumber(line.quantity)}
                  </span>
                  <span>{formatNumber(line.lineTotal)}</span>
                </li>
              ))}
            </ul>
            <div className={styles.row}>
              <span>{messages.subtotal}</span>
              <span>{toman(order.subtotal)}</span>
            </div>
            <div className={styles.row}>
              <span>{messages.shipping}</span>
              <span>{order.shippingFee === 0 ? messages.shippingFree : toman(order.shippingFee)}</span>
            </div>
            <div className={`${styles.row} ${styles.totalRow}`}>
              <span>{messages.total}</span>
              <span>{toman(order.total)}</span>
            </div>
          </section>

          <section className={styles.card} aria-labelledby="address">
            <h2 id="address" className="ui-heading-4">
              {messages.deliveryAddress}
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
          </section>
        </div>
      </div>
    </div>
  );
}
