import Link from 'next/link';
import { adminGet } from '@/lib/admin/api';
import { adminMessages } from '@/lib/admin/messages';
import type { Dashboard } from '@/lib/admin/types';
import { formatNumber } from '@/lib/format';
import styles from '../admin.module.css';

export default async function DashboardPage() {
  const data = await adminGet<Dashboard>('/admin/dashboard');
  if (!data) throw new Error('The dashboard is unavailable.');
  const cards = [
    { label: adminMessages.dashboard.newOrders, value: data.newOrders, href: '/admin/orders?status=new' },
    { label: adminMessages.dashboard.transferWaiting, value: data.transferReferenceWaiting, href: '/admin/orders?status=awaitingPayment&ref=1' },
    { label: adminMessages.dashboard.toShip, value: data.confirmedToShip, href: '/admin/orders?status=confirmed' },
    { label: adminMessages.dashboard.today, value: data.ordersToday, href: '/admin/orders' },
    { label: adminMessages.dashboard.lowStock(formatNumber(data.lowStockThreshold)), value: data.lowStockVariants, href: '/admin/products' },
  ];
  return (
    <>
      <h1 className="ui-heading-2">{adminMessages.nav.dashboard}</h1>
      <div className={styles.cards}>
        {cards.map((card) => (
          <Link key={card.label} href={card.href} className={styles.card}>
            <span className={styles.cardNumber}>{formatNumber(card.value)}</span>
            <span>{card.label}</span>
          </Link>
        ))}
      </div>
    </>
  );
}
