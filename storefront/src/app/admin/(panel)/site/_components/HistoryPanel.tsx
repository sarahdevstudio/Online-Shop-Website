import Link from 'next/link';
import { Badge } from '@/components/Badge';
import { FormSection } from '@/components/form';
import { adminMessages } from '@/lib/admin/messages';
import { formatSiteTime } from '@/lib/admin/site-format';
import { siteMessages } from '@/lib/admin/site-messages';
import type { SiteKind, SiteVersionSummary } from '@/lib/admin/site-types';
import { formatNumber } from '@/lib/format';
import { RestoreForm } from './RestoreForm';
import styles from './site.module.css';

const t = siteMessages.history;

/** The last published versions of one kind: view, restore into the draft, restore and publish. The current version has no restore. */
export function HistoryPanel({ kind, history }: { kind: SiteKind; history: readonly SiteVersionSummary[] }) {
  return (
    <FormSection card title={t.title} hint={t.hint}>
      {history.length === 0 ? (
        <p className={styles.muted}>{t.empty}</p>
      ) : (
        <div className={styles.wrap} tabIndex={0} role="region" aria-label={adminMessages.scrollableTable}>
          <table className={styles.table}>
            <caption className="ui-visually-hidden">{t.title}</caption>
            <thead>
              <tr>
                <th scope="col">{t.version}</th>
                <th scope="col">{t.state}</th>
                <th scope="col">{t.when}</th>
                <th scope="col">{t.by}</th>
                <th scope="col">
                  <span className="ui-visually-hidden">{adminMessages.edit}</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {history.map((entry) => (
                <tr key={entry.version}>
                  <td className={styles.num}>{formatNumber(entry.version)}</td>
                  <td>{entry.state === 'published' ? <Badge variant="success">{t.current}</Badge> : <Badge>{t.archived}</Badge>}</td>
                  <td className={styles.num}>{formatSiteTime(entry.publishedAtUtc)}</td>
                  <td>{entry.publishedBy ?? t.system}</td>
                  <td>
                    <div className={styles.rowActions}>
                      <Link href={`/admin/site/versions/${kind}/${entry.version}`}>{siteMessages.actions.view}</Link>
                      {entry.state === 'archived' && <RestoreForm kind={kind} version={entry.version} />}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </FormSection>
  );
}
