import Link from 'next/link';
import { getSiteAudit } from '@/lib/admin/site-api';
import { siteMessages } from '@/lib/admin/site-messages';
import { isSiteKind } from '@/lib/admin/site-types';
import { formatNumber } from '@/lib/format';
import { AuditList } from '../_components/AuditList';
import styles from '../_components/site.module.css';

const t = siteMessages.audit;

export default async function SiteAuditPage({ searchParams }: { searchParams: Promise<{ kind?: string; page?: string }> }) {
  const query = await searchParams;
  const kind = isSiteKind(query.kind) ? query.kind : null;
  const page = Math.max(1, Number.parseInt(query.page ?? '1', 10) || 1);
  const audit = await getSiteAudit(kind, page);
  const pageCount = audit?.pageCount ?? 1;
  const href = (target: number) => `/admin/site/audit?${new URLSearchParams({ ...(kind ? { kind } : {}), page: String(target) })}`;

  return (
    <>
      <h1 className="ui-heading-2">{t.pageTitle}</h1>
      <p>
        <Link href="/admin/site">{siteMessages.title}</Link>
        {kind && <span className={styles.muted}> — {siteMessages.kinds[kind].title}</span>}
      </p>
      <AuditList items={audit?.items ?? []} showKind={!kind} />
      <nav aria-label={t.title} className={styles.pager}>
        {page > 1 && <Link href={href(page - 1)}>{t.prev}</Link>}
        <span>{t.pageOf(formatNumber(audit?.page ?? page), formatNumber(pageCount))}</span>
        {page < pageCount && <Link href={href(page + 1)}>{t.next}</Link>}
      </nav>
    </>
  );
}
