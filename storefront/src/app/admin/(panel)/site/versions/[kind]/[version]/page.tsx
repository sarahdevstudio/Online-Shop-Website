import Link from 'next/link';
import { notFound } from 'next/navigation';
import { Badge } from '@/components/Badge';
import { adminMessages } from '@/lib/admin/messages';
import { getSiteVersion } from '@/lib/admin/site-api';
import { formatSiteTime } from '@/lib/admin/site-format';
import { siteMessages } from '@/lib/admin/site-messages';
import { SITE_KIND_PATH, isSiteKind } from '@/lib/admin/site-types';
import { formatNumber } from '@/lib/format';
import { RestoreForm } from '../../../_components/RestoreForm';
import styles from '../../../_components/site.module.css';

const t = siteMessages.history;

export default async function SiteVersionPage({ params }: { params: Promise<{ kind: string; version: string }> }) {
  const { kind, version: rawVersion } = await params;
  const version = Number(rawVersion);
  if (!isSiteKind(kind) || !Number.isSafeInteger(version) || version <= 0) notFound();
  const doc = await getSiteVersion(kind, version);
  if (!doc) notFound();

  return (
    <>
      <h1 className="ui-heading-2">{t.versionTitle(siteMessages.kinds[kind].title, formatNumber(version))}</h1>
      <div className={styles.strip}>
        {doc.state === 'published' ? <Badge variant="success">{t.current}</Badge> : <Badge>{t.archived}</Badge>}
        <span className={styles.stripText}>
          {formatSiteTime(doc.publishedAtUtc)} · {doc.publishedBy ?? t.system}
        </span>
        <Link href={SITE_KIND_PATH[kind]}>{t.backToEditor}</Link>
      </div>
      {doc.state === 'archived' && <RestoreForm kind={kind} version={version} />}
      <section aria-labelledby="payload" className={styles.section}>
        <h2 id="payload" className="ui-heading-4">
          {t.payloadHeading}
        </h2>
        <pre className={styles.json} tabIndex={0} role="region" aria-label={adminMessages.scrollableTable}>
          {JSON.stringify(doc.payload, null, 2)}
        </pre>
      </section>
    </>
  );
}
