import { pageMetadata } from '@/lib/seo';

export const metadata = pageMetadata({ title: 'سبد خرید', description: 'سبد خرید', path: '/cart', noindex: true });

export default function CartPage() {
  return (
    <div className="ui-container ui-section">
      <h1 className="ui-heading-2">سبد خرید</h1>
      <p className="ui-text-muted">این بخش در فاز بعدی پیاده‌سازی می‌شود.</p>
    </div>
  );
}
