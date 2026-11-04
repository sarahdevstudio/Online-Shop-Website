import type { Metadata } from 'next';
import Link from 'next/link';
import { messages } from '@/lib/messages';

/** What every 410 response shows: the proxy rewrites a permanently deleted path to this page and sets the status. */
export const metadata: Metadata = { robots: { index: false, follow: true } };

export default function GonePage() {
  return (
    <div className="ui-container ui-section ui-stack">
      <h1 className="ui-heading-2">{messages.goneTitle}</h1>
      <p>{messages.goneText}</p>
      <Link href="/">{messages.notFoundBack}</Link>
    </div>
  );
}
