import Link from 'next/link';
import { notFound } from 'next/navigation';
import { adminGet } from '@/lib/admin/api';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminBlogPost, BlogAuditEntry } from '@/lib/admin/types';
import { formatJalaliDate } from '@/lib/format';
import { BlogEditor, DeleteBlogForm } from '../BlogEditor';
import styles from '../../../admin.module.css';

const t = adminMessages.blog;

export default async function EditBlogPostPage({ params }: { params: Promise<{ id: string }> }) {
  const raw = (await params).id;
  if (!/^[1-9]\d{0,9}$/.test(raw)) notFound();
  const [post, audit] = await Promise.all([
    adminGet<AdminBlogPost>(`/admin/blog/${raw}`),
    adminGet<BlogAuditEntry[]>(`/admin/blog/${raw}/audit`),
  ]);
  if (!post) notFound();

  return (
    <>
      <div className={styles.row}>
        <h1 className="ui-heading-2">{post.title}</h1>
        {post.isPublished && (
          <Link href={`/blog/${post.slug}`} target="_blank" rel="noopener">
            {t.viewOnSite}
          </Link>
        )}
      </div>
      <p className={styles.muted}>
        <span className={`${styles.badge} ${post.isPublished ? styles.ok : styles.flag}`}>{post.isPublished ? t.published : t.draft}</span>{' '}
        <bdi dir="ltr">/blog/{post.slug}</bdi>
        {post.publishedAtUtc && <> — {t.publishedAt}: {formatJalaliDate(post.publishedAtUtc)}</>}
      </p>
      <BlogEditor post={post} />

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
                {formatJalaliDate(entry.atUtc)} — <bdi dir="ltr">{entry.adminUsername ?? adminMessages.none}</bdi> — {t.actions[entry.action] ?? adminMessages.none}
              </li>
            ))}
          </ul>
        )}
      </section>

      <DeleteBlogForm id={post.id} slug={post.slug} />
    </>
  );
}
