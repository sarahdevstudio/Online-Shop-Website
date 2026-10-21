import Link from 'next/link';

export default function NotFound() {
  return (
    <div className="ui-container ui-section ui-stack">
      <h1 className="ui-heading-2">صفحه پیدا نشد</h1>
      <Link href="/">بازگشت به صفحه اصلی</Link>
    </div>
  );
}
