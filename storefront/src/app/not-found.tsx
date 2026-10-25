import Link from 'next/link';
import { ShopChrome } from '@/components/ShopChrome';

/** Unmatched URLs and `notFound()` calls: the shop's header and footer around a short message. */
export default function NotFound() {
  return (
    <ShopChrome>
      <div className="ui-container ui-section ui-stack">
        <h1 className="ui-heading-2">صفحه پیدا نشد</h1>
        <Link href="/">بازگشت به صفحه اصلی</Link>
      </div>
    </ShopChrome>
  );
}
