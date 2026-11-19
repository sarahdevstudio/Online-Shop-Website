import Link from 'next/link';
import { adminGet } from '@/lib/admin/api';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminDiscountListItem } from '@/lib/admin/types';
import { formatDigits, formatJalaliDate, formatNumber, formatPercent } from '@/lib/format';
import { messages } from '@/lib/messages';
import styles from '../../admin.module.css';

const t = adminMessages.discounts;

function statusLabel(d: AdminDiscountListItem): string {
  if (!d.isActive) return t.statusInactive;
  if (d.expiresAtUtc && new Date(d.expiresAtUtc) <= new Date()) return t.statusExpired;
  return t.statusActive;
}

export default async function DiscountsListPage() {
  const codes = (await adminGet<AdminDiscountListItem[]>('/admin/discounts')) ?? [];
  return (
    <>
      <div className={styles.row}>
        <h1 className="ui-heading-2">{t.title}</h1>
        <Link href="/admin/discounts/new">{t.newCode}</Link>
      </div>
      <p className={styles.hint}>{t.hint}</p>
      {codes.length === 0 ? (
        <p className={styles.muted}>{t.noCodes}</p>
      ) : (
        <div className={styles.tableWrap} tabIndex={0} role="region" aria-label={adminMessages.scrollableTable}>
          <table className={styles.table}>
            <thead>
              <tr>
                <th>{t.code}</th>
                <th>{t.type}</th>
                <th>{t.value}</th>
                <th>{t.status}</th>
                <th>{t.used}</th>
                <th>{t.expiresAt}</th>
              </tr>
            </thead>
            <tbody>
              {codes.map((d) => (
                <tr key={d.id}>
                  <td>
                    <Link href={`/admin/discounts/${d.id}`} className={styles.ltr}>
                      {d.code}
                    </Link>
                  </td>
                  <td>{d.type === 'percent' ? t.typePercent : t.typeFixed}</td>
                  <td className={styles.num}>{d.type === 'percent' ? formatPercent(d.value / 100) : `${formatNumber(d.value)} ${messages.currencyUnit}`}</td>
                  <td>
                    <span className={`${styles.badge} ${statusLabel(d) === t.statusActive ? styles.ok : styles.flag}`}>{statusLabel(d)}</span>
                  </td>
                  <td className={styles.num}>
                    {formatDigits(d.usedCount)}
                    {d.usageLimit !== null ? ` / ${formatDigits(d.usageLimit)}` : ''}
                  </td>
                  <td className={styles.num}>{d.expiresAtUtc ? formatJalaliDate(d.expiresAtUtc) : adminMessages.none}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}
