import { adminMessages } from '@/lib/admin/messages';
import type { ReportsDailyPoint } from '@/lib/admin/types';
import { formatDigits, formatJalaliDate, formatToman } from '@/lib/format';
import styles from './reports.module.css';

const t = adminMessages.reports;

/** x/y of each point, scaled into a 0..width / 0..height viewBox with a little top margin for the highest value. */
function scale(values: number[], width: number, height: number, margin = 8) {
  const max = Math.max(...values, 1);
  const step = values.length > 1 ? width / (values.length - 1) : 0;
  return values.map((v, i) => ({ x: values.length > 1 ? i * step : width / 2, y: margin + (height - margin * 2) * (1 - v / max) }));
}

function Line({ points, className }: { points: { x: number; y: number }[]; className: string }) {
  if (points.length === 0) return null;
  const d = points.length === 1
    ? `M ${points[0].x} ${points[0].y} L ${points[0].x} ${points[0].y}`
    : points.map((p, i) => `${i === 0 ? 'M' : 'L'} ${p.x} ${p.y}`).join(' ');
  return <path d={d} className={className} fill="none" />;
}

/** Revenue and profit over the period: two lines, plus the real numbers in an accessible table (collapsed by default). */
export function DailyChart({ daily }: { daily: ReportsDailyPoint[] }) {
  const width = 600;
  const height = 180;
  const revenuePoints = scale(daily.map((d) => d.revenue), width, height);
  const profitPoints = scale(daily.map((d) => d.profit), width, height);
  return (
    <div className={styles.chartBlock}>
      <h2 className="ui-heading-5">{t.dailyChartTitle}</h2>
      {daily.length === 0 ? (
        <p className={styles.muted}>{t.noOrders}</p>
      ) : (
        <>
          <svg viewBox={`0 0 ${width} ${height}`} className={styles.chart} role="img" aria-label={t.dailyChartTitle} preserveAspectRatio="none">
            <Line points={revenuePoints} className={styles.lineRevenue} />
            <Line points={profitPoints} className={styles.lineProfit} />
          </svg>
          <p className={styles.legend}>
            <span className={styles.legendRevenue}>■</span> {t.revenue} &nbsp;&nbsp;
            <span className={styles.legendProfit}>■</span> {t.profit}
          </p>
          <details>
            <summary>{adminMessages.scrollableTable}</summary>
            <div className={styles.tableWrap} tabIndex={0}>
              <table className={styles.table}>
                <thead>
                  <tr>
                    <th>{t.day}</th>
                    <th>{t.revenue}</th>
                    <th>{t.profit}</th>
                    <th>{t.orderCount}</th>
                  </tr>
                </thead>
                <tbody>
                  {daily.map((d) => (
                    <tr key={d.date}>
                      <td>{formatJalaliDate(`${d.date}T12:00:00Z`)}</td>
                      <td>{formatToman(d.revenue)}</td>
                      <td>{formatToman(d.profit)}</td>
                      <td>{formatDigits(d.orderCount)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </details>
        </>
      )}
    </div>
  );
}

/** Order count per day as bars, plus the same numbers in an accessible table. */
export function OrderCountChart({ daily }: { daily: ReportsDailyPoint[] }) {
  const width = 600;
  const height = 120;
  const max = Math.max(...daily.map((d) => d.orderCount), 1);
  const barWidth = daily.length > 0 ? Math.min(40, (width / daily.length) * 0.6) : 0;
  const gap = daily.length > 1 ? width / daily.length : 0;
  return (
    <div className={styles.chartBlock}>
      <h2 className="ui-heading-5">{t.orderCountChartTitle}</h2>
      {daily.length === 0 ? (
        <p className={styles.muted}>{t.noOrders}</p>
      ) : (
        <svg viewBox={`0 0 ${width} ${height}`} className={styles.chart} role="img" aria-label={t.orderCountChartTitle} preserveAspectRatio="none">
          {daily.map((d, i) => {
            const barHeight = (d.orderCount / max) * (height - 8);
            const x = gap > 0 ? i * gap + (gap - barWidth) / 2 : width / 2 - barWidth / 2;
            return <rect key={d.date} x={x} y={height - barHeight} width={barWidth} height={barHeight} className={styles.bar} />;
          })}
        </svg>
      )}
    </div>
  );
}
