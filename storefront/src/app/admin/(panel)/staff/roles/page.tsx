import Link from 'next/link';
import { adminGet } from '@/lib/admin/api';
import { adminMessages } from '@/lib/admin/messages';
import { staffMessages } from '@/lib/admin/staff-messages';
import { roleLabel, type StaffOverview } from '@/lib/admin/staff-types';
import { formatNumber } from '@/lib/format';
import styles from '../../../admin.module.css';

const t = staffMessages;

export default async function RolesPage() {
  const overview = await adminGet<StaffOverview>('/admin/staff');
  if (!overview) throw new Error('Staff management is unavailable.');
  const roles = overview.roles.filter((role) => role.name !== 'Customer');
  return (
    <>
      <p>
        <Link href="/admin/staff">{t.back}</Link>
      </p>
      <h1 className="ui-heading-2">{t.roles}</h1>
      <p>
        <Link href="/admin/staff/roles/new" className="ui-control" style={{ inlineSize: 'auto', textDecoration: 'none' }}>
          {t.newRole}
        </Link>
      </p>
      <div className={styles.tableWrap} tabIndex={0} role="region" aria-label={adminMessages.scrollableTable}>
        <table className={styles.table}>
          <thead>
            <tr>
              <th>{t.roleTitle}</th>
              <th>{t.rolePermissions}</th>
              <th>{t.members}</th>
            </tr>
          </thead>
          <tbody>
            {roles.map((role) => (
              <tr key={role.id}>
                <td>{role.isSystem ? roleLabel(role, t.roleNames()) : <Link href={`/admin/staff/roles/${role.id}`}>{roleLabel(role, t.roleNames())}</Link>}</td>
                <td>{role.isSystem ? t.systemRole : role.permissions.map(t.permissionLabel).join(adminMessages.listSeparator) || adminMessages.none}</td>
                <td className={styles.num}>{formatNumber(role.userCount)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}
