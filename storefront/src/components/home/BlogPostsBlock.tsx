import Link from 'next/link';
import { formatJalaliDate } from '@/lib/format';
import { messages } from '@/lib/messages';
import type { HomeBlock } from '@/lib/site-config';
import styles from './BlogPostsBlock.module.css';

type Data = Extract<HomeBlock, { type: 'blogPosts' }>['data'];

export function BlogPostsBlock({ data, headingId }: { data: Data; headingId: string }) {
  if (data.posts.length === 0) return null;

  return (
    <section className="ui-container ui-section" aria-labelledby={headingId}>
      <div className={styles.head}>
        <h2 id={headingId} className="ui-heading-3">
          {messages.homeLatestPosts}
        </h2>
        <Link className={styles.all} href="/blog">
          {messages.viewAll}
        </Link>
      </div>
      <ul className={styles.list}>
        {data.posts.map((post) => (
          <li key={post.slug} className={styles.card}>
            <h3 className="ui-heading-4">
              <Link href={post.path}>{post.title}</Link>
            </h3>
            <time className={styles.date} dateTime={post.publishedAtUtc}>
              {messages.publishedOn(formatJalaliDate(post.publishedAtUtc))}
            </time>
            {post.excerpt && <p className={styles.excerpt}>{post.excerpt}</p>}
          </li>
        ))}
      </ul>
    </section>
  );
}
