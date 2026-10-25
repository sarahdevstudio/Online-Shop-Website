import Link from 'next/link';
import { adminMessages } from '@/lib/admin/messages';

export default function AdminNotFound() {
  return (
    <main style={{ padding: '2rem', maxWidth: '40rem', margin: '0 auto' }}>
      <h1 className="ui-heading-3">{adminMessages.notFound}</h1>
      <Link href="/admin">{adminMessages.nav.dashboard}</Link>
    </main>
  );
}
