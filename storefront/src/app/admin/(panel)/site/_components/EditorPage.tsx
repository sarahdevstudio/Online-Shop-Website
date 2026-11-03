import type { ReactNode } from 'react';
import { siteMessages } from '@/lib/admin/site-messages';
import type { AdminSiteDocument, SiteAuditItem, SiteKind } from '@/lib/admin/site-types';
import { AuditList } from './AuditList';
import { HistoryPanel } from './HistoryPanel';
import styles from './site.module.css';

/** Page frame shared by every site editor: title, the editor, version history, recent audit. */
export function EditorPage({ kind, doc, audit, children }: { kind: SiteKind; doc: AdminSiteDocument<unknown>; audit: readonly SiteAuditItem[]; children: ReactNode }) {
  return (
    <>
      <h1 className="ui-heading-2">{siteMessages.kinds[kind].title}</h1>
      <p className={styles.muted}>{siteMessages.kinds[kind].description}</p>
      {children}
      <HistoryPanel kind={kind} history={doc.history} />
      <section aria-labelledby="audit" className={styles.section}>
        <h2 id="audit" className="ui-heading-4">
          {siteMessages.audit.title}
        </h2>
        <AuditList items={audit.slice(0, 10)} showKind={false} />
      </section>
    </>
  );
}
