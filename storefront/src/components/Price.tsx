import { Badge } from './Badge';
import { discountFraction, formatNumber, formatPercent } from '@/lib/format';
import { messages } from '@/lib/messages';
import styles from './Price.module.css';

interface PriceProps {
  /** Always server-supplied. null renders "call for price". */
  amount: number | null;
  compareAt?: number | null;
  unit?: string;
  size?: 'md' | 'lg';
  showBadge?: boolean;
}

export function Price({ amount, compareAt, unit, size = 'md', showBadge = true }: PriceProps) {
  if (amount === null) {
    return (
      <div className={styles.price}>
        <span className={styles.call}>{messages.callForPrice}</span>
      </div>
    );
  }
  const discount = discountFraction(amount, compareAt);
  return (
    <div className={`${styles.price} ${size === 'lg' ? styles.lg : ''}`}>
      <span className={styles.row}>
        <span className={styles.current}>
          <span className={styles.amount}>{formatNumber(amount)}</span>
          <span className={styles.unit}>{unit ?? messages.currencyUnit}</span>
        </span>
        {discount > 0 && showBadge && <Badge variant="sale">{messages.discountOff(formatPercent(discount))}</Badge>}
      </span>
      {discount > 0 && (
        <>
          <span className="ui-visually-hidden">{messages.previousPrice}:</span>
          <del className={styles.compare}>{formatNumber(compareAt ?? 0)}</del>
        </>
      )}
    </div>
  );
}
