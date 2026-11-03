import Link from 'next/link';
import { Badge } from '@/components/Badge';
import { getSiteAudit, getSiteOverview } from '@/lib/admin/site-api';
import { formatSiteTime } from '@/lib/admin/site-format';
import { siteMessages } from '@/lib/admin/site-messages';
import { SITE_KIND_PATH, SITE_KINDS } from '@/lib/admin/site-types';
import { formatNumber } from '@/lib/format';
import { AuditList } from './_components/AuditList';
import styles from './_components/site.module.css';

export default async function SiteOverviewPage() {
  const [overview, audit] = await Promise.all([getSiteOverview(), getSiteAudit(null, 1)]);
  const byKind = new Map((overview ?? []).map((item) => [item.kind, item]));

  return (
    <>
      <h1 className="ui-heading-2">{siteMessages.title}</h1>
      <p className={styles.muted}>{siteMessages.intro}</p>

      <ul className={styles.cards}>
        {SITE_KINDS.map((kind) => {
          const item = byKind.get(kind);
          const text = siteMessages.kinds[kind];
          return (
            <li key={kind} className={styles.kindCard}>
              <h2>
                <Link href={SITE_KIND_PATH[kind]}>{text.title}</Link>
              </h2>
              <p>{text.description}</p>
              <div className={styles.strip}>
                {item?.publishedVersion ? (
                  <Badge variant="success">{`${siteMessages.state.published} · ${siteMessages.state.version(formatNumber(item.publishedVersion))}`}</Badge>
                ) : (
                  <Badge>{siteMessages.state.never}</Badge>
                )}
                {item?.hasDraft ? <Badge variant="warning">{siteMessages.state.draft}</Badge> : <Badge>{siteMessages.state.noDraft}</Badge>}
              </div>
              <div className={styles.meta}>
                <span className={styles.muted}>
                  {item?.hasDraft ? `${siteMessages.state.draft}: ${formatSiteTime(item.draftUpdatedAtUtc)}` : `${siteMessages.state.lastChange}: ${formatSiteTime(item?.publishedAtUtc)}`}
                </span>
              </div>
            </li>
          );
        })}
      </ul>

      <section aria-labelledby="recent" className={styles.section}>
        <h2 id="recent" className="ui-heading-4">
          {siteMessages.audit.recent}
        </h2>
        <AuditList items={(audit?.items ?? []).slice(0, 10)} />
        <p>
          <Link href="/admin/site/audit">{siteMessages.audit.viewAll}</Link>
        </p>
      </section>
    </>
  );
}
