import { pageMetadata } from '@/lib/seo';

export const metadata = pageMetadata({ title: 'حساب کاربری', description: 'حساب کاربری', path: '/account', noindex: true });

export default function AccountPage() {
  return (
    <div className="ui-container ui-section">
      <h1 className="ui-heading-2">حساب کاربری</h1>
      <p className="ui-text-muted">این بخش در فاز بعدی پیاده‌سازی می‌شود.</p>
    </div>
  );
}
