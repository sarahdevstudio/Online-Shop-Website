import Link from 'next/link';
import { notFound } from 'next/navigation';
import { SeoPanel } from '../../../_components/SeoPanel';
import type { AdminSeo } from '@/lib/admin/seo-types';
import { adminGet } from '@/lib/admin/api';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminContentPage, ContentAuditEntry, RouteTarget } from '@/lib/admin/types';
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
  const [route, seo] = await Promise.all([adminGet<RouteTarget>(`/admin/routes/contentPage/${page.id}`), adminGet<AdminSeo>(`/admin/seo/contentPage/${page.id}`)]);
  if (!route) notFound();

  return (
    <>
      <div className={styles.row}>
        <h1 className="ui-heading-2">{page.title}</h1>
        <Link href={route.path} target="_blank" rel="noopener">
          {t.viewOnSite}
        </Link>
      </div>
      <ContentEditor page={page} route={route} />
      {seo && <SeoPanel data={seo} />}

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
                {formatJalaliDate(entry.atUtc)} — <bdi dir="ltr">{entry.adminUsername ?? adminMessages.none}</bdi> —{' '}
                {[entry.titleChanged && t.titleChanged, entry.bodyChanged && t.bodyChanged].filter(Boolean).join(adminMessages.listSeparator)}
              </li>
            ))}
          </ul>
        )}
      </section>
    </>
  );
}
