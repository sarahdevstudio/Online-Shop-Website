import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { Breadcrumb } from '@/components/Breadcrumb';
import { JsonLd } from '@/components/JsonLd';
import { getBlogPost, isBlogSlug } from '@/lib/blog';
import { formatJalaliDate } from '@/lib/format';
import { renderMarkdown } from '@/lib/markdown';
import { messages } from '@/lib/messages';
import { absoluteUrl, breadcrumbJsonLd, pageMetadata, site } from '@/lib/seo';
import styles from '../blog.module.css';

type Props = { params: Promise<{ slug: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const slug = (await params).slug;
  if (!isBlogSlug(slug)) return {};
  const post = await getBlogPost(slug);
  if (!post) return {};
  return pageMetadata({ title: post.title, description: post.excerpt, path: `/blog/${post.slug}`, type: 'article' });
}

export default async function BlogPostPage({ params }: Props) {
  const slug = (await params).slug;
  if (!isBlogSlug(slug)) notFound();
  const post = await getBlogPost(slug);
  if (!post) notFound();

  const path = `/blog/${post.slug}`;
  const crumbs = [{ label: messages.home, href: '/' }, { label: messages.blog, href: '/blog' }, { label: post.title }];

  return (
    <article className="ui-container ui-section ui-stack">
      <JsonLd
        data={{
          '@context': 'https://schema.org',
          '@type': 'BlogPosting',
          headline: post.title,
          description: post.excerpt,
          datePublished: post.publishedAtUtc,
          dateModified: post.updatedAtUtc,
          inLanguage: 'fa-IR',
          mainEntityOfPage: absoluteUrl(path),
          publisher: { '@type': 'Organization', name: site.name, url: site.url },
        }}
      />
      <JsonLd data={breadcrumbJsonLd([{ label: messages.home, href: '/' }, { label: messages.blog, href: '/blog' }, { label: post.title, href: path }])} />
      <Breadcrumb items={crumbs} />
      <h1 className="ui-heading-2">{post.title}</h1>
      <time className={styles.date} dateTime={post.publishedAtUtc}>
        {messages.publishedOn(formatJalaliDate(post.publishedAtUtc))}
      </time>
      {/* Safe by construction: raw HTML is disabled and links are filtered (see lib/markdown.ts). */}
      <div className={`ui-prose ${styles.body}`} dangerouslySetInnerHTML={{ __html: renderMarkdown(post.body) }} />
    </article>
  );
}
