import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { Breadcrumb } from '@/components/Breadcrumb';
import { JsonLd } from '@/components/JsonLd';
import { getContentPage, type InfoSlug } from '@/lib/content';
import { messages } from '@/lib/messages';
import { plainDescription, renderMarkdown } from '@/lib/markdown';
import { contactJsonLd, pageMetadata, withSeoOverrides } from '@/lib/seo';
import { getEntitySeo } from '@/lib/entity-seo';
import { seoDefaults } from '@/lib/site-config';
import { getStoreInfo } from '@/lib/store-info';
import styles from './info-page.module.css';

type Props = { slug: InfoSlug };

// The five info pages (about, contact, terms, returns, privacy); where they live is decided by the route table.
export async function infoPageMetadata({ slug }: Props): Promise<Metadata> {
  const page = await getContentPage(slug);
  if (!page) return {};
  // Indexable and self-canonical.
  const defaults = await seoDefaults();
  const seo = await getEntitySeo('contentPage', slug);
  return pageMetadata(withSeoOverrides({ title: page.title, description: plainDescription(page.body) || page.title, path: page.path, siteName: defaults.siteName, image: defaults.image }, seo));
}

export async function InfoPageView({ slug }: Props) {
  const page = await getContentPage(slug);
  if (!page) notFound();

  const info = slug === 'contact' ? await getStoreInfo() : null;
  const hasDetails = info && (info.storePhone || info.storeAddress || info.workingHours || info.instagramHandle);

  return (
    <div className="ui-container ui-section ui-stack">
      <Breadcrumb items={[{ label: messages.home, href: '/' }, { label: page.title }]} />
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
