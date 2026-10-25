import type { Metadata } from 'next';
import type { ReactNode } from 'react';
import { adminMessages } from '@/lib/admin/messages';

// Never indexed: metadata here, X-Robots-Tag from the proxy and next.config.ts, Disallow in robots.txt. Never cached, and no
// Referer is sent from admin pages.
export const metadata: Metadata = {
  title: adminMessages.title,
  robots: { index: false, follow: false, nocache: true },
  referrer: 'no-referrer',
};

// Admin pages depend on the request (cookies) and on live API data: never prerendered, never cached.
export const dynamic = 'force-dynamic';

export default function AdminRootLayout({ children }: { children: ReactNode }) {
  return <>{children}</>;
}
