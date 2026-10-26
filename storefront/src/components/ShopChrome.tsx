import type { ReactNode } from 'react';
import { JsonLd } from '@/components/JsonLd';
import { SiteFooter } from '@/components/SiteFooter';
import { SiteHeader } from '@/components/SiteHeader';
import { getCategoryTree } from '@/lib/catalog';
import { getInfoLinks } from '@/lib/content';
import { messages } from '@/lib/messages';
import { organizationJsonLd, site, websiteJsonLd } from '@/lib/seo';
import { getCartCount } from '@/lib/store';
import { getStoreInfo } from '@/lib/store-info';

/** The public shop's header, footer and structured data around a page. The admin panel has its own shell. */
export async function ShopChrome({ children }: { children: ReactNode }) {
  const categories = await getCategoryTree();
  const [cartCount, info, infoLinks] = await Promise.all([getCartCount(), getStoreInfo(), getInfoLinks()]);
  const categoryItems = categories.map((c) => ({ label: c.name, href: `/category/${c.slug}` }));
  const navItems = [...categoryItems, { label: messages.blog, href: '/blog' }];

  return (
    <>
      <JsonLd data={organizationJsonLd()} />
      <JsonLd data={websiteJsonLd()} />
      <SiteHeader brandName={site.name} navItems={navItems} cartCount={cartCount} />
      <main id="main" tabIndex={-1}>
        {children}
      </main>
      <SiteFooter
        brandName={site.name}
        about={site.description}
        groups={[
          { title: messages.categories, links: categoryItems },
          { title: messages.infoPages, links: [...infoLinks, { label: messages.blog, href: '/blog' }] },
        ]}
        phone={info?.storePhone || undefined}
        address={info?.storeAddress || undefined}
        hours={info?.workingHours || undefined}
        instagram={info?.instagramHandle || undefined}
        copyright={`© ${site.name}`}
      />
    </>
  );
}
