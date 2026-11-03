import type { Metadata } from 'next';
import type { ReactNode } from 'react';
import { adminMessages } from '@/lib/admin/messages';
import { loadAdminTexts } from '@/lib/i18n/admin-texts';
import { TextsProvider } from '@/lib/i18n/TextsProvider';

// Never indexed: metadata here, X-Robots-Tag from the proxy and next.config.ts, Disallow in robots.txt. Never cached, and no
// Referer is sent from admin pages.
export async function generateMetadata(): Promise<Metadata> {
  await loadAdminTexts().catch(() => undefined);
  return {
    title: adminMessages.title,
    robots: { index: false, follow: false, nocache: true },
    referrer: 'no-referrer',
  };
}

// Admin pages depend on the request (cookies) and on live API data: never prerendered, never cached.
export const dynamic = 'force-dynamic';

export default async function AdminRootLayout({ children }: { children: ReactNode }) {
  // The panel's own texts need the admin session; the login page (no session yet) uses the shipped texts.
  const texts = await loadAdminTexts().catch(() => ({}));
  return <TextsProvider texts={texts}>{children}</TextsProvider>;
}
