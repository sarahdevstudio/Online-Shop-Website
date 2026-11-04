import Link from 'next/link';
import { adminGet, requireAdmin } from '@/lib/admin/api';
import { adminMessages } from '@/lib/admin/messages';
import { staffMessages } from '@/lib/admin/staff-messages';
import { roleLabel, type StaffAuditPage, type StaffOverview } from '@/lib/admin/staff-types';
import { formatJalaliDate, formatNumber } from '@/lib/format';
import type { SearchParams } from '@/lib/paging';
import styles from '../../admin.module.css';

const t = staffMessages;

export default async function StaffPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const sp = await searchParams;
  const raw = Array.isArray(sp.auditPage) ? sp.auditPage[0] : sp.auditPage;
  const page = Math.max(Number.parseInt(raw ?? '1', 10) || 1, 1);
  const [session, overview, audit] = await Promise.all([
    requireAdmin(),
    adminGet<StaffOverview>('/admin/staff'),
    adminGet<StaffAuditPage>(`/admin/staff/audit?page=${page}`),
  ]);
  if (!overview || !audit) throw new Error('Staff management is unavailable.');
  const lastPage = Math.max(Math.ceil(audit.total / 50), 1);
  const byName = new Map(overview.roles.map((role) => [role.name, role]));

  return (
    <>
      <h1 className="ui-heading-2">{t.title}</h1>
      <p>{t.intro}</p>
      <p>
        <Link href="/admin/staff/new" className="ui-control" style={{ inlineSize: 'auto', textDecoration: 'none' }}>
          {t.newUser}
        </Link>{' '}
        <Link href="/admin/staff/roles">{t.manageRoles}</Link>
      </p>

      <section className={styles.panel} aria-labelledby="users">
        <h2 id="users" className="ui-heading-4">
          {t.users}
        </h2>
        <div className={styles.tableWrap} tabIndex={0} role="region" aria-label={`${adminMessages.scrollableTable}: ${t.users}`}>
          <table className={styles.table}>
            <thead>
              <tr>
                <th>{t.colUsername}</th>
                <th>{t.colName}</th>
                <th>{t.colRoles}</th>
                <th>{t.colState}</th>
              </tr>
            </thead>
            <tbody>
              {overview.users.map((user) => (
                <tr key={user.id}>
                  <td className={styles.ltr}>
                    <Link href={`/admin/staff/${user.id}`}>{user.username}</Link>
                    {user.username === session.username && <span className={styles.muted}> ({t.me})</span>}
                  </td>
                  <td>{user.displayName ?? adminMessages.none}</td>
                  <td>{user.roles.map((name) => (byName.has(name) ? roleLabel(byName.get(name)!, t.roleNames()) : name)).join(adminMessages.listSeparator)}</td>
                  <td>{user.isActive ? t.active : <span className={styles.badge}>{t.inactive}</span>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <section className={styles.panel} aria-labelledby="audit">
        <h2 id="audit" className="ui-heading-4">
          {t.audit}
        </h2>
        {audit.items.length === 0 ? (
          <p>{t.noAudit}</p>
        ) : (
          <div className={styles.tableWrap} tabIndex={0} role="region" aria-label={`${adminMessages.scrollableTable}: ${t.audit}`}>
            <table className={styles.table}>
              <thead>
                <tr>
                  <th>{t.auditWhen}</th>
                  <th>{t.auditWho}</th>
                  <th>{t.auditAction}</th>
                  <th>{t.auditSubject}</th>
                  <th>{t.auditDetail}</th>
                </tr>
              </thead>
              <tbody>
                {audit.items.map((e) => (
                  <tr key={e.id}>
                    <td className={styles.num}>{formatJalaliDate(e.atUtc)}</td>
                    <td className={styles.ltr}>{e.actorName ?? adminMessages.none}</td>
                    <td>{t.actionLabel(e.action)}</td>
                    <td className={styles.ltr}>{e.subject ?? adminMessages.none}</td>
                    <td className={styles.ltr}>{e.detail ?? adminMessages.none}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        {lastPage > 1 && (
          <nav className={styles.pager}>
            {page > 1 && <Link href={`/admin/staff?auditPage=${page - 1}`}>{'‹'}</Link>}
            <span>
              {formatNumber(page)} / {formatNumber(lastPage)}
            </span>
            {page < lastPage && <Link href={`/admin/staff?auditPage=${page + 1}`}>{'›'}</Link>}
          </nav>
        )}
      </section>
    </>
  );
}
