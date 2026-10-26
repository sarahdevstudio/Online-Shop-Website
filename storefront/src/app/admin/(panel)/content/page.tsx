import Link from 'next/link';
import { adminGet } from '@/lib/admin/api';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminContentListItem } from '@/lib/admin/types';
import { formatJalaliDate } from '@/lib/format';
import styles from '../../admin.module.css';

const t = adminMessages.content;

export default async function ContentListPage() {
  const pages = (await adminGet<AdminContentListItem[]>('/admin/content-pages')) ?? [];
  return (
    <>
      <h1 className="ui-heading-2">{t.title}</h1>
      <p className={styles.hint}>{t.hint}</p>
      <div className={styles.tableWrap}>
        <table className={styles.table}>
          <thead>
            <tr>
              <th>{adminMessages.products.name}</th>
              <th>{adminMessages.products.status}</th>
              <th>{t.lastEdit}</th>
              <th>{t.by}</th>
            </tr>
          </thead>
          <tbody>
            {pages.map((page) => (
              <tr key={page.slug}>
                <td>
                  <Link href={`/admin/content/${page.slug}`}>{page.title}</Link> <span className={`${styles.muted} ${styles.ltr}`}>/{page.slug}</span>
                </td>
                <td>
                  <span className={`${styles.badge} ${page.isDraft ? styles.flag : styles.ok}`}>{page.isDraft ? t.draft : t.published}</span>
                </td>
                <td className={styles.num}>{formatJalaliDate(page.updatedAtUtc)}</td>
                <td className={styles.ltr}>{page.updatedByEmail ?? adminMessages.none}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}
