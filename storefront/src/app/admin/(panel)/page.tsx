import Link from 'next/link';
import { adminGet } from '@/lib/admin/api';
import { adminMessages } from '@/lib/admin/messages';
import type { Dashboard } from '@/lib/admin/types';
import { formatNumber, formatToman } from '@/lib/format';
import styles from '../admin.module.css';

const t = adminMessages.dashboard;

/** A tiny inline sparkline (no chart library): one polyline, no axes, just the shape of the last 30 days. */
function Sparkline({ values }: { values: number[] }) {
  const width = 200;
  const height = 40;
  const max = Math.max(...values, 1);
  const step = values.length > 1 ? width / (values.length - 1) : 0;
  const points = values.map((v, i) => `${i * step},${height - (v / max) * (height - 4) - 2}`).join(' ');
  return (
    <svg viewBox={`0 0 ${width} ${height}`} className={styles.sparkline} role="img" aria-label={t.sparklineLabel} preserveAspectRatio="none">
      <polyline points={points} fill="none" />
    </svg>
  );
}

export default async function DashboardPage() {
  const data = await adminGet<Dashboard>('/admin/dashboard');
  if (!data) throw new Error('The dashboard is unavailable.');

  const cards: { label: string; value: string; href: string }[] = [];
  if (data.orders) {
    cards.push(
      { label: t.newOrders, value: formatNumber(data.orders.newOrders), href: '/admin/orders?status=new' },
      { label: t.transferWaiting, value: formatNumber(data.orders.transferReferenceWaiting), href: '/admin/orders?status=awaitingPayment&ref=1' },
      { label: t.toShip, value: formatNumber(data.orders.confirmedToShip), href: '/admin/orders?status=confirmed' },
      { label: t.today, value: formatNumber(data.orders.ordersToday), href: '/admin/orders' },
    );
  }

  if (data.reports) {
    cards.push(
      { label: t.todayRevenue, value: formatToman(data.reports.todayRevenue), href: '/admin/reports?preset=7' },
      { label: t.todayProfit, value: formatToman(data.reports.todayProfit), href: '/admin/reports?preset=7' },
      { label: t.monthRevenue, value: formatToman(data.reports.monthRevenue), href: '/admin/reports?preset=30' },
      { label: t.monthProfit, value: formatToman(data.reports.monthProfit), href: '/admin/reports?preset=30' },
    );
  }

  if (data.pendingReviews !== null) {
    cards.push({ label: t.pendingReviews, value: formatNumber(data.pendingReviews), href: '/admin/reviews?status=pending' });
  }

  if (data.lowStock) {
    cards.push({ label: t.lowStock(formatNumber(data.lowStock.threshold)), value: formatNumber(data.lowStock.count), href: '/admin/products' });
  }

  if (data.draftProducts !== null) {
    cards.push({ label: t.draftProducts, value: formatNumber(data.draftProducts), href: '/admin/products?status=draft' });
  }

  return (
    <>
      <h1 className="ui-heading-2">{adminMessages.nav.dashboard}</h1>
      <div className={styles.cards}>
        {cards.map((card) => (
          <Link key={card.label} href={card.href} className={styles.card}>
            <span className={styles.cardNumber}>{card.value}</span>
            <span>{card.label}</span>
          </Link>
        ))}
      </div>

      {data.reports && data.reports.sparkline30d.some((v) => v > 0) && (
        <section className={styles.panel}>
          <h2 className="ui-heading-4">{t.sparklineLabel}</h2>
          <Sparkline values={data.reports.sparkline30d} />
        </section>
      )}

      <nav className={styles.panel} aria-label={t.quickActions}>
        <h2 className="ui-heading-4">{t.quickActions}</h2>
        <div className={styles.quickActions}>
          {data.draftProducts !== null && <Link href="/admin/products/new">{t.newProduct}</Link>}
          {data.orders && <Link href="/admin/orders">{t.viewOrders}</Link>}
          {data.draftProducts !== null && <Link href="/admin/products/transfer">{t.transferProducts}</Link>}
        </div>
      </nav>
    </>
  );
}
