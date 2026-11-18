import Link from 'next/link';
import { Form, SubmitButton } from '@/components/form';
import { adminGet } from '@/lib/admin/api';
import { adminMessages } from '@/lib/admin/messages';
import { lastNDaysRangeUtc, tehranDayRangeUtc, tehranIsoDate } from '@/lib/admin/tehran';
import type { Reports } from '@/lib/admin/types';
import { formatDigits, formatPercent, formatToman } from '@/lib/format';
import type { SearchParams } from '@/lib/paging';
import { DailyChart, OrderCountChart } from './ReportsCharts';
import adminStyles from '../../admin.module.css';
import styles from './reports.module.css';

const t = adminMessages.reports;
const first = (value: string | string[] | undefined) => (Array.isArray(value) ? value[0] : value) ?? '';
const PRESETS = [7, 30, 90] as const;

export default async function ReportsPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const sp = await searchParams;
  const preset = first(sp.preset);
  const fromParam = first(sp.from);
  const toParam = first(sp.to);

  const now = new Date();
  let fromUtc: Date;
  let toUtc: Date;
  let activePreset = 7;
  if (fromParam && toParam) {
    fromUtc = tehranDayRangeUtc(fromParam).fromUtc;
    toUtc = tehranDayRangeUtc(toParam).toUtc;
    activePreset = 0;
  } else {
    activePreset = PRESETS.includes(Number(preset) as 7 | 30 | 90) ? Number(preset) : 7;
    ({ fromUtc, toUtc } = lastNDaysRangeUtc(activePreset, now));
  }

  const query = new URLSearchParams({ from: fromUtc.toISOString(), to: toUtc.toISOString() });
  const report = await adminGet<Reports>(`/admin/reports?${query}`);
  if (!report) throw new Error('Reports are unavailable.');
  const { summary } = report;

  return (
    <>
      <h1 className="ui-heading-2">{t.title}</h1>

      <nav className={styles.filters} aria-label={t.title}>
        {PRESETS.map((days) => (
          <Link key={days} href={`/admin/reports?preset=${days}`} className={styles.presetLink} aria-current={activePreset === days ? 'true' : undefined}>
            {days === 7 ? t.last7 : days === 30 ? t.last30 : t.last90}
          </Link>
        ))}
        <Form method="get" className={styles.customForm} inline>
          <label>
            {t.from}
            <input type="date" name="from" defaultValue={tehranIsoDate(fromUtc)} required />
          </label>
          <label>
            {t.to}
            <input type="date" name="to" defaultValue={tehranIsoDate(new Date(toUtc.getTime() - 24 * 3600_000))} required />
          </label>
          <SubmitButton variant="secondary">{t.apply}</SubmitButton>
        </Form>
      </nav>

      {summary.ordersWithoutCost > 0 && (
        <p className={styles.warning}>{t.withoutCostWarning(formatDigits(summary.ordersWithoutCost), formatDigits(summary.itemsWithoutCost))}</p>
      )}

      <div className={adminStyles.cards}>
        <div className={adminStyles.card}><span>{t.revenue}</span><strong className={adminStyles.cardNumber}>{formatToman(summary.revenue)}</strong></div>
        <div className={adminStyles.card}><span>{t.shippingCollected}</span><strong className={adminStyles.cardNumber}>{formatToman(summary.shippingCollected)}</strong></div>
        <div className={adminStyles.card}><span>{t.costOfGoods}</span><strong className={adminStyles.cardNumber}>{formatToman(summary.costOfGoods)}</strong></div>
        <div className={adminStyles.card}><span>{t.grossProfit}</span><strong className={adminStyles.cardNumber}>{formatToman(summary.grossProfit)}</strong></div>
        <div className={adminStyles.card}><span>{t.margin}</span><strong className={adminStyles.cardNumber}>{formatPercent(summary.marginPercent / 100)}</strong></div>
        <div className={adminStyles.card}><span>{t.orderCount}</span><strong className={adminStyles.cardNumber}>{formatDigits(summary.orderCount)}</strong></div>
        <div className={adminStyles.card}><span>{t.averageOrderValue}</span><strong className={adminStyles.cardNumber}>{formatToman(summary.averageOrderValue)}</strong></div>
      </div>

      <DailyChart daily={report.daily} />
      <OrderCountChart daily={report.daily} />

      <section className={styles.chartBlock} aria-labelledby="top-revenue">
        <h2 id="top-revenue" className="ui-heading-5">{t.topByRevenue}</h2>
        <ProductTable products={report.topByRevenue} metric="revenue" />
      </section>

      <section className={styles.chartBlock} aria-labelledby="top-profit">
        <h2 id="top-profit" className="ui-heading-5">{t.topByProfit}</h2>
        <ProductTable products={report.topByProfit} metric="profit" />
      </section>
    </>
  );
}

function ProductTable({ products, metric }: { products: Reports['topByRevenue']; metric: 'revenue' | 'profit' }) {
  if (products.length === 0) return <p className={styles.muted}>{t.noOrders}</p>;
  return (
    <div className={styles.tableWrap} tabIndex={0}>
      <table className={styles.table}>
        <thead>
          <tr>
            <th>{t.product}</th>
            <th>{metric === 'revenue' ? t.revenue : t.profit}</th>
          </tr>
        </thead>
        <tbody>
          {products.map((p) => (
            <tr key={p.productId}>
              <td>
                <Link href={`/admin/products/${p.productId}`}>{p.productName}</Link>
              </td>
              <td>{formatToman(metric === 'revenue' ? p.revenue : p.profit)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
