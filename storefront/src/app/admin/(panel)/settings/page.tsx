import Link from 'next/link';
import { adminGet } from '@/lib/admin/api';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminSettings, PagedList, SettingsAuditEntry } from '@/lib/admin/types';
import { formatJalaliDate, formatNumber } from '@/lib/format';
import type { SearchParams } from '@/lib/paging';
import { SettingsForm } from './SettingsForm';
import styles from '../../admin.module.css';

const t = adminMessages.settings;

export default async function SettingsPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const sp = await searchParams;
  const raw = Array.isArray(sp.auditPage) ? sp.auditPage[0] : sp.auditPage;
  const page = Math.max(Number.parseInt(raw ?? '1', 10) || 1, 1);
  const [settings, audit] = await Promise.all([
    adminGet<AdminSettings>('/admin/settings'),
    adminGet<PagedList<SettingsAuditEntry>>(`/admin/settings/audit?page=${page}`),
  ]);
  if (!settings || !audit) throw new Error('Settings are unavailable.');
  const lastPage = Math.max(Math.ceil(audit.total / audit.pageSize), 1);

  return (
    <>
      <h1 className="ui-heading-2">{t.title}</h1>
      <SettingsForm data={settings} />

      <section className={styles.panel} aria-labelledby="audit">
        <h2 id="audit" className="ui-heading-4">
          {t.audit}
        </h2>
        {audit.items.length === 0 ? (
          <p>{t.noAudit}</p>
        ) : (
          <div className={styles.tableWrap}>
            <table className={styles.table}>
              <thead>
                <tr>
                  <th>{t.when}</th>
                  <th>{t.who}</th>
                  <th>{t.field}</th>
                  <th>{t.old}</th>
                  <th>{t.new}</th>
                </tr>
              </thead>
              <tbody>
                {audit.items.map((e) => (
                  <tr key={e.id}>
                    <td className={styles.num}>{formatJalaliDate(e.atUtc)}</td>
                    <td className={styles.ltr}>{e.adminEmail ?? adminMessages.none}</td>
                    <td>{t.fields[e.field] ?? e.field}</td>
                    <td className={styles.ltr}>{e.oldValue ?? adminMessages.none}</td>
                    <td className={styles.ltr}>{e.newValue ?? adminMessages.none}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {lastPage > 1 && (
          <nav className={styles.pager}>
            {page > 1 && <Link href={`/admin/settings?auditPage=${page - 1}`}>{'‹'}</Link>}
            <span>
              {formatNumber(page)} / {formatNumber(lastPage)}
            </span>
            {page < lastPage && <Link href={`/admin/settings?auditPage=${page + 1}`}>{'›'}</Link>}
          </nav>
        )}
      </section>
    </>
  );
}
