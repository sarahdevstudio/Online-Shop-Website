import Link from 'next/link';
import { adminGet } from '@/lib/admin/api';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminBlogListItem } from '@/lib/admin/types';
import { formatJalaliDate } from '@/lib/format';
import styles from '../../admin.module.css';

const t = adminMessages.blog;

export default async function BlogListPage() {
  const posts = (await adminGet<AdminBlogListItem[]>('/admin/blog')) ?? [];
  return (
    <>
      <div className={styles.row}>
        <h1 className="ui-heading-2">{t.title}</h1>
        <Link href="/admin/blog/new">{t.newPost}</Link>
      </div>
      <p className={styles.hint}>{t.hint}</p>
      {posts.length === 0 ? (
        <p className={styles.muted}>{t.noPosts}</p>
      ) : (
        <div className={styles.tableWrap}>
          <table className={styles.table}>
            <thead>
              <tr>
                <th>{t.postTitle}</th>
                <th>{adminMessages.products.status}</th>
                <th>{t.lastEdit}</th>
                <th>{t.by}</th>
              </tr>
            </thead>
            <tbody>
              {posts.map((post) => (
                <tr key={post.id}>
                  <td>
                    <Link href={`/admin/blog/${post.id}`}>{post.title}</Link> <span className={`${styles.muted} ${styles.ltr}`}>/{post.slug}</span>
                  </td>
                  <td>
                    <span className={`${styles.badge} ${post.isPublished ? styles.ok : styles.flag}`}>{post.isPublished ? t.published : t.draft}</span>
                  </td>
                  <td className={styles.num}>{formatJalaliDate(post.updatedAtUtc)}</td>
                  <td className={styles.ltr}>{post.updatedByUsername ?? adminMessages.none}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}
