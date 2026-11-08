import Link from 'next/link';
import { adminGet } from '@/lib/admin/api';
import { adminMessages } from '@/lib/admin/messages';
import { adminReviewMessages as t } from '@/lib/admin/review-messages';
import type { AdminReviewList, ReviewAuditPage, ReviewStatus } from '@/lib/admin/review-types';
import { formatJalaliDate, formatNumber } from '@/lib/format';
import type { SearchParams } from '@/lib/paging';
import { ModerateButtons } from './ModerateButtons';
import styles from '../../admin.module.css';

const STATUSES: readonly ReviewStatus[] = ['pending', 'approved', 'rejected'];

export default async function ReviewsPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const sp = await searchParams;
  const raw = Array.isArray(sp.status) ? sp.status[0] : sp.status;
  const all = raw === 'all';
  const status: ReviewStatus = STATUSES.find((s) => s === raw) ?? 'pending';
  const page = Math.max(Number.parseInt((Array.isArray(sp.page) ? sp.page[0] : sp.page) ?? '1', 10) || 1, 1);

  const [list, audit] = await Promise.all([
    adminGet<AdminReviewList>(`/admin/reviews?${all ? '' : `status=${status}&`}page=${page}`),
    adminGet<ReviewAuditPage>('/admin/reviews/audit'),
  ]);
  if (!list || !audit) throw new Error('Reviews are unavailable.');
  const lastPage = Math.max(Math.ceil(list.total / list.pageSize), 1);
  const tabs: { key: string; label: string }[] = [
    { key: 'pending', label: t.tabPending(formatNumber(list.pending)) },
    { key: 'approved', label: t.tabApproved(formatNumber(list.approved)) },
    { key: 'rejected', label: t.tabRejected(formatNumber(list.rejected)) },
    { key: 'all', label: t.tabAll },
  ];
  const current = all ? 'all' : status;
  const statusLabel: Record<ReviewStatus, string> = { pending: t.statusPending, approved: t.statusApproved, rejected: t.statusRejected };

  return (
    <>
      <h1 className="ui-heading-2">{t.title}</h1>
      <p>{t.intro}</p>
      <nav aria-label={t.title}>
        <ul className={styles.nav}>
          {tabs.map((tab) => (
            <li key={tab.key}>
              <Link href={`/admin/reviews?status=${tab.key}`} aria-current={tab.key === current ? 'page' : undefined}>
                {tab.label}
              </Link>
            </li>
          ))}
        </ul>
      </nav>

      {list.items.length === 0 ? (
        <p>{t.empty}</p>
      ) : (
        <div className={styles.tableWrap} tabIndex={0} role="region" aria-label={`${adminMessages.scrollableTable}: ${t.title}`}>
          <table className={styles.table}>
            <thead>
              <tr>
                <th>{t.colProduct}</th>
                <th>{t.colAuthor}</th>
                <th>{t.colRating}</th>
                <th>{t.colText}</th>
                <th>{t.colDate}</th>
                <th>{t.colStatus}</th>
                <th>{t.colActions}</th>
              </tr>
            </thead>
            <tbody>
              {list.items.map((review) => (
                <tr key={review.id}>
                  <td>
                    <Link href={`/product/${review.productSlug}`} target="_blank" rel="noopener">
                      {review.productName}
                    </Link>
                  </td>
                  <td>
                    {review.name}
                    <br />
                    <span className={`${styles.muted} ${styles.ltr}`}>{review.mobile}</span>
                  </td>
                  <td className={styles.num}>{t.stars(formatNumber(review.rating))}</td>
                  <td>{review.text}</td>
                  <td className={styles.num}>{formatJalaliDate(review.createdAtUtc.endsWith('Z') ? review.createdAtUtc : `${review.createdAtUtc}Z`)}</td>
                  <td>
                    {statusLabel[review.status]}
                    {review.moderatedBy && (
                      <>
                        <br />
                        <span className={styles.muted}>
                          {t.by} <bdi dir="ltr">{review.moderatedBy}</bdi>
                        </span>
                      </>
                    )}
                  </td>
                  <td>
                    <ModerateButtons review={review} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {lastPage > 1 && (
        <nav className={styles.pager}>
          {page > 1 && <Link href={`/admin/reviews?status=${current}&page=${page - 1}`}>{'‹'}</Link>}
          <span>
            {formatNumber(page)} / {formatNumber(lastPage)}
          </span>
          {page < lastPage && <Link href={`/admin/reviews?status=${current}&page=${page + 1}`}>{'›'}</Link>}
        </nav>
      )}

      <section className={styles.panel} aria-labelledby="audit">
        <h2 id="audit" className="ui-heading-4">
          {t.audit}
        </h2>
        {audit.items.length === 0 ? (
          <p>{t.auditEmpty}</p>
        ) : (
          <ul>
            {audit.items.map((entry) => (
              <li key={entry.id}>
                {formatJalaliDate(entry.atUtc.endsWith('Z') ? entry.atUtc : `${entry.atUtc}Z`)} — <bdi dir="ltr">{entry.adminUsername ?? adminMessages.none}</bdi> — {entry.action === 'approved' ? t.actionApproved : t.actionRejected} — {entry.productName}
              </li>
            ))}
          </ul>
        )}
      </section>
    </>
  );
}
