import type { Metadata } from 'next';
import Link from 'next/link';
import { notFound, permanentRedirect } from 'next/navigation';
import { Pagination } from '@/components/Pagination';
import { getBlogList } from '@/lib/blog';
import { formatJalaliDate } from '@/lib/format';
import { messages } from '@/lib/messages';
import { isPageInRange, pageOneRedirect, parsePageParam, type SearchParams } from '@/lib/paging';
import { pageMetadata } from '@/lib/seo';
import styles from './blog.module.css';

type Props = { searchParams: Promise<SearchParams> };

export async function generateMetadata({ searchParams }: Props): Promise<Metadata> {
  const query = await searchParams;
  const page = typeof query.page === 'string' || query.page === undefined ? parsePageParam(query.page) : null;
  // Every page is indexable and canonical to itself; page 1 is the clean URL.
  return pageMetadata({
    title: page && page > 1 ? `${messages.blogTitle} (صفحه ${page})` : messages.blogTitle,
    description: messages.blogDescription,
    path: page && page > 1 ? `/blog?page=${page}` : '/blog',
  });
}

export default async function BlogListPage({ searchParams }: Props) {
  const query = await searchParams;
  const redirectTo = pageOneRedirect('/blog', query);
  if (redirectTo) permanentRedirect(redirectTo);

  const page = typeof query.page === 'string' || query.page === undefined ? parsePageParam(query.page) : null;
  if (page === null) notFound();
  const list = await getBlogList(page);
  if (!isPageInRange(page, list.totalPages)) notFound();

  return (
    <div className="ui-container ui-section ui-stack">
      <h1 className="ui-heading-2">{messages.blogTitle}</h1>
      {list.items.length === 0 ? (
        <p>{messages.blogEmpty}</p>
      ) : (
        <ul className={styles.list}>
          {list.items.map((post) => (
            <li key={post.slug} className={styles.card}>
              <h2 className="ui-heading-4">
                <Link href={`/blog/${post.slug}`}>{post.title}</Link>
              </h2>
              <time className={styles.date} dateTime={post.publishedAtUtc}>
                {messages.publishedOn(formatJalaliDate(post.publishedAtUtc))}
              </time>
              <p className={styles.excerpt}>{post.excerpt}</p>
              <Link href={`/blog/${post.slug}`} className={styles.more}>
                {messages.readMore}
              </Link>
            </li>
          ))}
        </ul>
      )}
      <Pagination page={list.page} pageCount={list.totalPages} basePath="/blog" />
    </div>
  );
}
