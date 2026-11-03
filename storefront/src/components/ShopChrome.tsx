import type { ReactNode } from 'react';
import { AnnouncementBar } from '@/components/AnnouncementBar';
import { JsonLd } from '@/components/JsonLd';
import { SiteFooter, type FooterGroup } from '@/components/SiteFooter';
import { SiteHeader } from '@/components/SiteHeader';
import { TrustBadges } from '@/components/TrustBadges';
import { getCategoryTree } from '@/lib/catalog';
import { getInfoLinks } from '@/lib/content';
import { messages } from '@/lib/messages';
import { organizationJsonLd, websiteJsonLd } from '@/lib/seo';
import { buildMenu } from '@/lib/site-menu';
import { SOCIAL_PLATFORMS, getSiteConfig } from '@/lib/site-config';
import { getCartCount } from '@/lib/store';
import { getStoreInfo } from '@/lib/store-info';

/** The public shop's header, footer and structured data around a page. The admin panel has its own shell. */
export async function ShopChrome({ children }: { children: ReactNode }) {
  const [categories, config] = await Promise.all([getCategoryTree(), getSiteConfig()]);
  const [cartCount, info, infoLinks] = await Promise.all([getCartCount(), getStoreInfo(), getInfoLinks()]);
  const { seo, chrome, theme } = config;

  const categoryLinks = categories.map((c) => ({ label: c.name, href: `/category/${c.slug}` }));
  const groups: FooterGroup[] = [
    { title: messages.categories, links: categoryLinks },
    ...chrome.footer.columns.map((column) => ({ title: column.title, links: column.links.map((l) => ({ label: l.label, href: l.link })) })),
    { title: messages.infoPages, links: [...infoLinks, { label: messages.blog, href: '/blog' }] },
  ];
  const social = SOCIAL_PLATFORMS.flatMap((platform) => {
    const href = chrome.social[platform];
    return href ? [{ label: messages.socialNames[platform], href }] : [];
  });
  const instagram = info?.instagramHandle || undefined;
  const sameAs = [...social.map((s) => s.href), ...(instagram ? [`https://www.instagram.com/${instagram}/`] : [])];
  const announcement = chrome.announcement;

  return (
    <>
      <JsonLd data={{ ...organizationJsonLd(), name: seo.siteName, ...(sameAs.length > 0 ? { sameAs } : {}) }} />
      <JsonLd data={{ ...websiteJsonLd(), name: seo.siteName }} />
      {announcement.enabled && <AnnouncementBar text={announcement.text} href={announcement.link} style={announcement.style} dismissible={announcement.dismissible} />}
      <SiteHeader brandName={seo.siteName} logo={theme.logo} menu={buildMenu(chrome.headerMenu, categories)} cartCount={cartCount} />
      <main id="main" tabIndex={-1}>
        {children}
      </main>
      <SiteFooter
        brandName={seo.siteName}
        about={chrome.footer.about}
        groups={groups}
        phone={info?.storePhone || undefined}
        address={info?.storeAddress || undefined}
        hours={info?.workingHours || undefined}
        instagram={instagram}
        social={social}
        copyright={`© ${seo.siteName}`}
        badges={config.badges.enamad || config.badges.samandehi ? <TrustBadges badges={config.badges} /> : undefined}
      />
    </>
  );
}
