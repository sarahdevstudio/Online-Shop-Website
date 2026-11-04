import type { Metadata, Viewport } from 'next';
import type { ReactNode } from 'react';
import { requestNonce } from '@/lib/nonce';
import { ThemeHead } from '@/components/ThemeHead';
import { TextsProvider } from '@/lib/i18n/TextsProvider';
import { clientTexts, ensureTexts } from '@/lib/i18n/texts-server';
import { getPreviewConfig, getSiteConfig } from '@/lib/site-config';
import { site } from '@/lib/seo';
import { themeColor } from '@/lib/theme-css';
import './globals.css';

/** Site-wide defaults come from the published site configuration (built-in values when it is unavailable). */
export async function generateMetadata(): Promise<Metadata> {
  await ensureTexts();
  const [{ seo, theme }, preview] = await Promise.all([getSiteConfig(), getPreviewConfig()]);
  return {
    robots: preview ? { index: false, follow: false } : undefined,
    metadataBase: new URL(site.url),
    title: { default: seo.homeTitle, template: seo.titleTemplate },
    description: seo.defaultDescription,
    openGraph: {
      siteName: seo.siteName,
      locale: site.locale,
      type: 'website',
      images: seo.defaultOgImage ? [{ url: seo.defaultOgImage.url, width: seo.defaultOgImage.width, height: seo.defaultOgImage.height }] : undefined,
    },
    icons: theme.favicon
      ? {
          icon: [
            { url: '/site-icon/32', sizes: '32x32', type: 'image/png' },
            { url: '/site-icon/192', sizes: '192x192', type: 'image/png' },
          ],
          apple: [{ url: '/site-icon/180', sizes: '180x180', type: 'image/png' }],
        }
      : undefined,
  };
}

export async function generateViewport(): Promise<Viewport> {
  const { theme } = await getSiteConfig();
  return { width: 'device-width', initialScale: 1, themeColor: themeColor(theme) };
}

/** Only the document shell. The public shop's header/footer are in `(shop)/layout.tsx`, the admin panel's in `admin/layout.tsx`. */
export default async function RootLayout({ children }: { children: ReactNode }) {
  await ensureTexts();
  const { theme } = await getSiteConfig();
  return (
    <html lang="fa" dir="rtl">
      <head>
        <ThemeHead theme={theme} nonce={await requestNonce()} />
      </head>
      <body>
        <TextsProvider texts={clientTexts(['common.', 'shop.', 'seo.'])}>{children}</TextsProvider>
      </body>
    </html>
  );
}
