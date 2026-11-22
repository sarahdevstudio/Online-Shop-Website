import Link from 'next/link';
import { adminMessages } from '@/lib/admin/messages';
import { getDraftCartAction } from './actions';
import { NewOrderForm } from './NewOrderForm';

export default async function NewOrderPage() {
  const cart = await getDraftCartAction();
  const t = adminMessages.orders.newOrder;
  return (
    <>
      <p>
        <Link href="/admin/orders">{t.back}</Link>
      </p>
      <h1 className="ui-heading-2">{t.title}</h1>
      <NewOrderForm initial={cart} />
    </>
  );
}
