import Link from 'next/link';
import { ShopChrome } from '@/components/ShopChrome';
import { messages } from '@/lib/messages';

/** Unmatched URLs and `notFound()` calls: the shop's header and footer around a short message. */
export default function NotFound() {
  return (
    <ShopChrome>
      <div className="ui-container ui-section ui-stack">
        <h1 className="ui-heading-2">{messages.notFoundTitle}</h1>
        <Link href="/">{messages.notFoundBack}</Link>
      </div>
    </ShopChrome>
  );
}
