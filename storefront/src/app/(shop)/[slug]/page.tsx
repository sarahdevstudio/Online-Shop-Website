import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { JsonLd } from '@/components/JsonLd';
import { getContentPage, isInfoSlug } from '@/lib/content';
import { messages } from '@/lib/messages';
import { plainDescription, renderMarkdown } from '@/lib/markdown';
import { contactJsonLd, pageMetadata } from '@/lib/seo';
import { getStoreInfo } from '@/lib/store-info';
import styles from './info-page.module.css';

type Props = { params: Promise<{ slug: string }> };

// Only the five info pages (about, contact, terms, returns, privacy) live here; any other single-segment URL is a 404.
export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const slug = (await params).slug;
  if (!isInfoSlug(slug)) return {};
  const page = await getContentPage(slug);
  if (!page) return {};
  // Indexable and self-canonical.
  return pageMetadata({ title: page.title, description: plainDescription(page.body) || page.title, path: `/${slug}` });
}

export default async function InfoPage({ params }: Props) {
  const slug = (await params).slug;
  if (!isInfoSlug(slug)) notFound();
  const page = await getContentPage(slug);
  if (!page) notFound();

  const info = slug === 'contact' ? await getStoreInfo() : null;
  const hasDetails = info && (info.storePhone || info.storeAddress || info.workingHours || info.instagramHandle);

  return (
    <div className="ui-container ui-section ui-stack">
      <h1 className="ui-heading-2">{page.title}</h1>
      {/* Safe by construction: raw HTML is disabled and links are filtered (see lib/markdown.ts). */}
      <div className={`ui-prose ${styles.body}`} dangerouslySetInnerHTML={{ __html: renderMarkdown(page.body) }} />

      {info && (
        <>
          <JsonLd
            data={contactJsonLd({
              phone: info.storePhone,
              address: info.storeAddress,
              hours: info.workingHours,
              instagram: info.instagramHandle,
            })}
          />
          {hasDetails && (
            <section className={styles.details} aria-labelledby="contact-details">
              <h2 id="contact-details" className="ui-heading-4">
                {messages.contactDetails}
              </h2>
              <dl>
                {info.storePhone && (
                  <div>
                    <dt>{messages.phone}</dt>
                    <dd>
                      <a href={`tel:${info.storePhone}`}>
                        <bdi dir="ltr">{info.storePhone}</bdi>
                      </a>
                    </dd>
                  </div>
                )}
                {info.storeAddress && (
                  <div>
                    <dt>{messages.addressLabel}</dt>
                    <dd>{info.storeAddress}</dd>
                  </div>
                )}
                {info.workingHours && (
                  <div>
                    <dt>{messages.workingHours}</dt>
                    <dd>{info.workingHours}</dd>
                  </div>
                )}
                {info.instagramHandle && (
                  <div>
                    <dt>{messages.instagram}</dt>
                    <dd>
                      <a href={`https://www.instagram.com/${info.instagramHandle}/`} rel="noopener noreferrer nofollow" target="_blank">
                        <bdi dir="ltr">@{info.instagramHandle}</bdi>
                      </a>
                    </dd>
                  </div>
                )}
              </dl>
            </section>
          )}
        </>
      )}
    </div>
  );
}
