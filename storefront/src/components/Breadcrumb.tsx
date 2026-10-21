import Link from 'next/link';
import { Icon } from './Icon';
import { JsonLd } from './JsonLd';
import { breadcrumbJsonLd } from '@/lib/seo';
import { messages } from '@/lib/messages';
import styles from './Breadcrumb.module.css';

export interface BreadcrumbItem {
  label: string;
  /** Omit for the current page (last item). */
  href?: string;
}

/** Visual trail plus BreadcrumbList JSON-LD from the same items, so both always agree. */
export function Breadcrumb({ items }: { items: readonly BreadcrumbItem[] }) {
  return (
    <nav aria-label={messages.breadcrumb}>
      <JsonLd data={breadcrumbJsonLd(items)} />
      <ol className={styles.list}>
        {items.map((item, index) => {
          const last = index === items.length - 1;
          return (
            <li key={`${item.label}-${index}`} className={styles.item}>
              {!last && item.href ? (
                <>
                  <Link href={item.href}>{item.label}</Link>
                  <Icon name="chevron-start" className={styles.sep} />
                </>
              ) : (
                <span aria-current={last ? 'page' : undefined}>{item.label}</span>
              )}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}
