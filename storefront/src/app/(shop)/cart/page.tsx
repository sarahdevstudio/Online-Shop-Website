import Image from 'next/image';
import Link from 'next/link';
import { Alert } from '@/components/Alert';
import { Button, ButtonLink } from '@/components/Button';
import { formatNumber } from '@/lib/format';
import { messages } from '@/lib/messages';
import type { SearchParams } from '@/lib/paging';
import { pageMetadata } from '@/lib/seo';
import { getCart } from '@/lib/store';
import type { CartLineDto } from '@/lib/store-types';
import { removeItemAction, updateQuantityAction } from './actions';
import styles from './cart.module.css';

export const metadata = pageMetadata({ title: 'سبد خرید', description: 'سبد خرید', path: '/cart', noindex: true });

function first(value: string | string[] | undefined): string | undefined {
  return Array.isArray(value) ? value[0] : value;
}

function LineNotice({ line }: { line: CartLineDto }) {
  if (line.notice === 'unavailable') return <Alert variant="danger">{messages.noticeUnavailable}</Alert>;
  if (line.notice === 'quantity_exceeds') return <Alert variant="warning">{messages.noticeExceeds(formatNumber(line.maxQuantity))}</Alert>;
  if (line.notice === 'price_changed' && line.previousPrice !== null) {
    return <Alert variant="info">{messages.noticePriceChanged(formatNumber(line.previousPrice), formatNumber(line.unitPrice))}</Alert>;
  }
  return null;
}

export default async function CartPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const params = await searchParams;
  const cart = await getCart();
  const errorKey = first(params.error);
  const noticeKey = first(params.notice);
  const shortIds = new Set((first(params.short) ?? '').split(',').map(Number));
  const shortNames = cart.lines.filter((l) => shortIds.has(l.variantId)).map((l) => l.productName);

  return (
    <div className="ui-container ui-section ui-stack">
      <h1 className="ui-heading-2">{messages.cart}</h1>

      {errorKey && messages.cartErrors[errorKey] && (
        <Alert variant="danger">
          <p>{messages.cartErrors[errorKey]}</p>
          {shortNames.length > 0 && (
            <p>
              {messages.shortItems} {shortNames.join('، ')}
            </p>
          )}
        </Alert>
      )}
      {noticeKey && messages.notices[noticeKey] && <Alert variant="info">{messages.notices[noticeKey]}</Alert>}

      {cart.lines.length === 0 ? (
        <div className="ui-stack">
          <h2 className="ui-heading-4">{messages.cartEmptyTitle}</h2>
          <p className="ui-text-muted">{messages.cartEmptyText}</p>
          <div>
            <ButtonLink href="/">{messages.continueShopping}</ButtonLink>
          </div>
        </div>
      ) : (
        <div className={styles.layout}>
          <ul className={styles.lines} aria-label={messages.cartItems}>
            {cart.lines.map((line) => (
              <li key={line.variantId} className={styles.line}>
                {line.imageUrl ? (
                  <Image className={styles.thumb} src={line.imageUrl} alt="" width={80} height={80} />
                ) : (
                  <div className={styles.noThumb} aria-hidden="true" />
                )}
                <div className={styles.info}>
                  <Link className={styles.name} href={`/product/${line.productSlug}`}>
                    {line.productName}
                  </Link>
                  {line.variantLabel && <span className={styles.meta}>{line.variantLabel}</span>}
                  <span className={styles.meta}>
                    {formatNumber(line.unitPrice)} {messages.currencyUnit}
                  </span>
                  <LineNotice line={line} />
                  <div className={styles.controls}>
                    {line.available && (
                      <form action={updateQuantityAction} className={styles.controls}>
                        <input type="hidden" name="variantId" value={line.variantId} />
                        <label>
                          <span className="ui-visually-hidden">{messages.quantity}</span>
                          <input
                            className={`ui-control ${styles.qty}`}
                            type="number"
                            name="quantity"
                            inputMode="numeric"
                            min={1}
                            max={line.maxQuantity}
                            defaultValue={line.quantity}
                          />
                        </label>
                        <Button type="submit" variant="secondary" size="sm">
                          {messages.cartUpdate}
                        </Button>
                      </form>
                    )}
                    <form action={removeItemAction} className={styles.inline}>
                      <input type="hidden" name="variantId" value={line.variantId} />
                      <Button type="submit" variant="ghost" size="sm" aria-label={messages.cartRemoveItem(line.productName)}>
                        {messages.cartRemove}
                      </Button>
                    </form>
                    {line.available && (
                      <span className={styles.lineTotal}>
                        {formatNumber(line.lineTotal)} {messages.currencyUnit}
                      </span>
                    )}
                  </div>
                </div>
              </li>
            ))}
          </ul>

          <aside className={styles.summary} aria-label={messages.orderSummary}>
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
            {cart.freeShippingThreshold !== null && cart.subtotal < cart.freeShippingThreshold && (
              <p className="ui-text-sm ui-text-muted">{messages.freeShippingHint(formatNumber(cart.freeShippingThreshold - cart.subtotal))}</p>
            )}
            <div className={`${styles.row} ${styles.totalRow}`}>
              <span>{messages.total}</span>
              <span>
                {formatNumber(cart.total)} {messages.currencyUnit}
              </span>
            </div>
            {cart.hasBlockingIssue ? (
              <Alert variant="warning">{messages.fixCartFirst}</Alert>
            ) : (
              <ButtonLink href="/checkout" block size="lg">
                {messages.proceedToCheckout}
              </ButtonLink>
            )}
          </aside>
        </div>
      )}
    </div>
  );
}
