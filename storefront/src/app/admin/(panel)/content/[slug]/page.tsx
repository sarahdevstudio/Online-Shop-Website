import Link from 'next/link';
import { notFound } from 'next/navigation';
import { adminGet } from '@/lib/admin/api';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminContentPage, ContentAuditEntry } from '@/lib/admin/types';
import { isInfoSlug } from '@/lib/content';
import { formatJalaliDate } from '@/lib/format';
import { ContentEditor } from '../ContentEditor';
import styles from '../../../admin.module.css';

const t = adminMessages.content;

export default async function EditContentPage({ params }: { params: Promise<{ slug: string }> }) {
  const slug = (await params).slug;
  if (!isInfoSlug(slug)) notFound();
  const [page, audit] = await Promise.all([
    adminGet<AdminContentPage>(`/admin/content-pages/${slug}`),
    adminGet<ContentAuditEntry[]>(`/admin/content-pages/${slug}/audit`),
  ]);
  if (!page) notFound();

  return (
    <>
      <div className={styles.row}>
        <h1 className="ui-heading-2">{page.title}</h1>
        <Link href={`/${slug}`} target="_blank" rel="noopener">
          {t.viewOnSite}
        </Link>
      </div>
      <ContentEditor page={page} />

      <section className={styles.panel} aria-labelledby="history">
        <h2 id="history" className="ui-heading-4">
          {t.history}
        </h2>
        {(audit ?? []).length === 0 ? (
          <p className={styles.muted}>{t.noHistory}</p>
        ) : (
          <ul>
            {(audit ?? []).map((entry, i) => (
              <li key={i}>
                {formatJalaliDate(entry.atUtc)} — <bdi dir="ltr">{entry.adminEmail ?? adminMessages.none}</bdi> —{' '}
                {[entry.titleChanged && t.titleChanged, entry.bodyChanged && t.bodyChanged].filter(Boolean).join('، ')}
              </li>
            ))}
          </ul>
        )}
      </section>
    </>
  );
}
