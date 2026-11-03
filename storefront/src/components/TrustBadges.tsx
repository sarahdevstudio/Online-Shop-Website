import { messages } from '@/lib/messages';
import type { Badges } from '@/lib/site-config';
import { enamadBadge, samandehiBadge } from '@/lib/trust-badges';
import styles from './TrustBadges.module.css';

/** The badges as the authorities' own anchor + image, built from a fixed template (see lib/trust-badges.ts). Footer only. */
export function TrustBadges({ badges }: { badges: Badges }) {
  const items = [
    { key: 'enamad', badge: enamadBadge(badges.enamad), alt: messages.enamadAlt },
    { key: 'samandehi', badge: samandehiBadge(badges.samandehi), alt: messages.samandehiAlt },
  ].flatMap((item) => (item.badge ? [{ ...item, badge: item.badge }] : []));
  if (items.length === 0) return null;

  return (
    <ul className={styles.badges} aria-label={messages.trustBadges}>
      {items.map(({ key, badge, alt }) => (
        <li key={key}>
          <a href={badge.href} target="_blank" rel="noopener" referrerPolicy="origin">
            {/* The visitor's browser loads the image from the authority, exactly like the official snippet. */}
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img src={badge.src} alt={alt} height={80} referrerPolicy="origin" loading="lazy" />
          </a>
        </li>
      ))}
    </ul>
  );
}
