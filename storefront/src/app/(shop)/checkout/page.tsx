import { randomUUID } from 'node:crypto';
import { redirect } from 'next/navigation';
import { formatNumber } from '@/lib/format';
import { messages } from '@/lib/messages';
import { pageMetadata } from '@/lib/seo';
import { getCart } from '@/lib/store';
import type { PaymentMethod } from '@/lib/store-types';
import { CheckoutForm } from './CheckoutForm';
import styles from './checkout.module.css';

export const metadata = pageMetadata({ title: 'ثبت سفارش', description: 'ثبت سفارش', path: '/checkout', noindex: true });

export default async function CheckoutPage() {
  const cart = await getCart();
  // Nothing to buy, or something the customer must fix first: the cart page explains.
  if (cart.lines.length === 0 || cart.hasBlockingIssue) redirect('/cart');

  const methods: PaymentMethod[] = [
    ...(cart.paymentMethods.cardToCard ? (['cardToCard'] as const) : []),
    ...(cart.paymentMethods.cashOnDelivery ? (['cashOnDelivery'] as const) : []),
  ];

  return (
    <div className="ui-container ui-section ui-stack">
      <h1 className="ui-heading-2">{messages.checkout}</h1>
      <div className={styles.layout}>
        <CheckoutForm idempotencyKey={randomUUID()} expectedTotal={cart.total} methods={methods} />

        <aside className={styles.summary} aria-label={messages.orderSummary}>
          <h2 className="ui-heading-4">{messages.orderSummary}</h2>
          <ul className={styles.summaryLines}>
            {cart.lines.map((line) => (
              <li key={line.variantId} className={styles.row}>
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
            <span>
              {formatNumber(cart.subtotal)} {messages.currencyUnit}
            </span>
          </div>
          <div className={styles.row}>
            <span>{messages.shipping}</span>
            <span>{cart.shippingFee === 0 ? messages.shippingFree : `${formatNumber(cart.shippingFee)} ${messages.currencyUnit}`}</span>
          </div>
          <div className={`${styles.row} ${styles.totalRow}`}>
            <span>{messages.total}</span>
            <span>
              {formatNumber(cart.total)} {messages.currencyUnit}
            </span>
          </div>
        </aside>
      </div>
    </div>
  );
}
