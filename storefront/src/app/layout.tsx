import type { Metadata, Viewport } from 'next';
import type { ReactNode } from 'react';
import { JsonLd } from '@/components/JsonLd';
import { SiteFooter } from '@/components/SiteFooter';
import { SiteHeader } from '@/components/SiteHeader';
import { getCategoryTree } from '@/lib/catalog';
import { messages } from '@/lib/messages';
import { getCartCount } from '@/lib/store';
import { organizationJsonLd, site, websiteJsonLd } from '@/lib/seo';
import './globals.css';

export const metadata: Metadata = {
  metadataBase: new URL(site.url),
  title: { default: `${site.name} | ${site.description}`, template: `%s | ${site.name}` },
  description: site.description,
  openGraph: { siteName: site.name, locale: site.locale, type: 'website' },
};

export const viewport: Viewport = {
  width: 'device-width',
  initialScale: 1,
  themeColor: '#ffffff',
};

export default async function RootLayout({ children }: { children: ReactNode }) {
  const categories = await getCategoryTree();
  const cartCount = await getCartCount();
  const navItems = categories.map((c) => ({ label: c.name, href: `/category/${c.slug}` }));

  return (
    <html lang="fa" dir="rtl">
      <body>
        <JsonLd data={organizationJsonLd()} />
        <JsonLd data={websiteJsonLd()} />
        <SiteHeader brandName={site.name} navItems={navItems} cartCount={cartCount} />
        <main id="main" tabIndex={-1}>
          {children}
        </main>
        <SiteFooter
          brandName={site.name}
          about={site.description}
          groups={[{ title: messages.categories, links: navItems }]}
          copyright={`© ${site.name}`}
        />
      </body>
    </html>
  );
}
