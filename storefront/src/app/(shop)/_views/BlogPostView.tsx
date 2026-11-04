import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { Breadcrumb } from '@/components/Breadcrumb';
import { JsonLd } from '@/components/JsonLd';
import { getBlogPost } from '@/lib/blog';
import { formatJalaliDate } from '@/lib/format';
import { renderMarkdown } from '@/lib/markdown';
import { messages } from '@/lib/messages';
import { absoluteUrl, pageMetadata, site, withSeoOverrides } from '@/lib/seo';
import { getEntitySeo } from '@/lib/entity-seo';
import { seoDefaults } from '@/lib/site-config';
import styles from '../blog/blog.module.css';

type Props = { slug: string };

export async function blogPostMetadata({ slug }: Props): Promise<Metadata> {
  const post = await getBlogPost(slug);
  if (!post) return {};
  const [defaults, seo] = await Promise.all([seoDefaults(), getEntitySeo('blogPost', slug)]);
  return pageMetadata(withSeoOverrides({ title: post.title, description: post.excerpt, path: post.path, type: 'article' as const, siteName: defaults.siteName, image: defaults.image }, seo));
}

export async function BlogPostView({ slug }: Props) {
  const post = await getBlogPost(slug);
  if (!post) notFound();

  const path = post.path;
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
