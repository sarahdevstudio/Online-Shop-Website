import Link from 'next/link';
import { adminMessages } from '@/lib/admin/messages';
import { formatSiteTime } from '@/lib/admin/site-format';
import { siteMessages } from '@/lib/admin/site-messages';
import type { SiteAuditItem } from '@/lib/admin/site-types';
import { formatNumber } from '@/lib/format';
import styles from './site.module.css';

const t = siteMessages.audit;

/** Who changed what and when (field names only; the API never stores the content). */
export function AuditList({ items, showKind = true }: { items: readonly SiteAuditItem[]; showKind?: boolean }) {
  if (items.length === 0) return <p className={styles.muted}>{t.empty}</p>;
  return (
    <div className={styles.wrap} tabIndex={0} role="region" aria-label={`${t.title} — ${adminMessages.scrollableTable}`}>
      <table className={styles.table}>
        <caption className="ui-visually-hidden">{t.title}</caption>
        <thead>
          <tr>
            <th scope="col">{t.when}</th>
            {showKind && <th scope="col">{t.kind}</th>}
            <th scope="col">{t.action}</th>
            <th scope="col">{t.version}</th>
            <th scope="col">{t.summary}</th>
            <th scope="col">{t.by}</th>
          </tr>
        </thead>
        <tbody>
          {items.map((item, index) => (
            <tr key={`${item.atUtc}-${index}`}>
              <td className={styles.num}>{formatSiteTime(item.atUtc)}</td>
              {showKind && (
                <td>
                  <Link href={`/admin/site/audit?kind=${item.kind}`}>{siteMessages.kinds[item.kind].title}</Link>
                </td>
              )}
              <td>{t.actions[item.action]}</td>
              <td className={styles.num}>{item.version === null ? adminMessages.none : formatNumber(item.version)}</td>
              <td>{item.summary || adminMessages.none}</td>
              <td>{item.adminUsername ?? siteMessages.history.system}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
