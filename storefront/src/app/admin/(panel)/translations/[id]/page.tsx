import Link from 'next/link';
import { notFound } from 'next/navigation';
import { adminGet } from '@/lib/admin/api';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminTranslation, TranslationAuditEntry } from '@/lib/admin/types';
import { formatJalaliDate } from '@/lib/format';
import { TranslationEditor } from '../TranslationEditor';
import styles from '../../../admin.module.css';

const t = adminMessages.translations;

export default async function EditTranslationPage({ params }: { params: Promise<{ id: string }> }) {
  const id = Number((await params).id);
  if (!Number.isSafeInteger(id) || id <= 0) notFound();
  const [row, audit] = await Promise.all([
    adminGet<AdminTranslation>(`/admin/translations/${id}`),
    adminGet<TranslationAuditEntry[]>(`/admin/translations/${id}/audit`),
  ]);
  if (!row) notFound();

  return (
    <>
      <div className={styles.row}>
        <h1 className="ui-heading-2">
          <bdi dir="ltr">{row.key}</bdi>
        </h1>
        <Link href="/admin/translations">{t.back}</Link>
      </div>
      <TranslationEditor row={row} />

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
                {formatJalaliDate(entry.atUtc)} — <bdi dir="ltr">{entry.adminUsername ?? adminMessages.none}</bdi>
                <br />
                {t.before}: {entry.oldText}
                <br />
                {t.after}: {entry.newText}
              </li>
            ))}
          </ul>
        )}
      </section>
    </>
  );
}
