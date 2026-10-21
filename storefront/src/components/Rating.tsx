import { Icon } from './Icon';
import { formatDecimal, formatNumber } from '@/lib/format';
import { messages } from '@/lib/messages';
import styles from './Rating.module.css';

const MAX = 5;
const SLOTS = [0, 1, 2, 3, 4];

/** Read-only stars; one text alternative for the whole control so meaning never depends on color. */
export function Rating({ value, count }: { value?: number | null; count?: number | null }) {
  if (value == null) {
    return (
      <span className={styles.rating}>
        <span className={styles.none}>{messages.noRating}</span>
      </span>
    );
  }
  const clamped = Math.min(Math.max(value, 0), MAX);
  const valueText = formatDecimal(clamped);
  const countText = count == null ? null : messages.ratingCount(formatNumber(count));
  const base = messages.rating(valueText, formatNumber(MAX));
  const stars = SLOTS.map((i) => <Icon key={i} name="star" filled />);
  return (
    <span className={styles.rating}>
      <span className={styles.stars} role="img" aria-label={countText ? `${base}، ${countText}` : base}>
        <span className={`${styles.layer} ${styles.empty}`} aria-hidden="true">{stars}</span>
        <span className={`${styles.layer} ${styles.full}`} aria-hidden="true" style={{ inlineSize: `${(clamped / MAX) * 100}%` }}>{stars}</span>
      </span>
      <span className={styles.value} aria-hidden="true">{valueText}</span>
      {countText && <span className={styles.count} aria-hidden="true">({countText})</span>}
    </span>
  );
}
