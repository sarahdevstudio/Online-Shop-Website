import Link from 'next/link';
import { adminGet } from '@/lib/admin/api';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminOrderListItem, PagedList } from '@/lib/admin/types';
import { formatJalaliDate, formatNumber } from '@/lib/format';
import { messages } from '@/lib/messages';
import type { SearchParams } from '@/lib/paging';
import styles from '../../admin.module.css';

const first = (value: string | string[] | undefined) => (Array.isArray(value) ? value[0] : value) ?? '';
const STATUSES = ['awaitingPayment', 'pending', 'confirmed', 'shipped', 'delivered', 'cancelled'];

async function load(status: string, paymentStatus: string, q: string, page: number): Promise<PagedList<AdminOrderListItem>> {
  const query = new URLSearchParams({ page: String(page) });
  if (status) query.set('status', status);
  if (paymentStatus) query.set('paymentStatus', paymentStatus);
  if (q) query.set('q', q);
  const result = await adminGet<PagedList<AdminOrderListItem>>(`/admin/orders?${query}`);
  if (!result) throw new Error('Orders are unavailable.');
  return result;
}

export default async function OrdersPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const sp = await searchParams;
  const status = first(sp.status);
  const paymentStatus = first(sp.paymentStatus);
  const q = first(sp.q).trim();
  const page = Math.max(Number.parseInt(first(sp.page), 10) || 1, 1);
  const onlyWithReference = first(sp.ref) === '1';

  let items: AdminOrderListItem[];
  let paging = true;
  let total: number;
  let pageSize = 20;
  if (status === 'new') {
    // "New" = waiting for payment confirmation or for the first review: two lists merged, newest first.
    const [pending, awaiting] = await Promise.all([load('pending', paymentStatus, q, 1), load('awaitingPayment', paymentStatus, q, 1)]);
    items = [...pending.items, ...awaiting.items].sort((a, b) => b.createdAtUtc.localeCompare(a.createdAtUtc));
    total = pending.total + awaiting.total;
    paging = false;
  } else {
    const result = await load(STATUSES.includes(status) ? status : '', paymentStatus, q, page);
    items = result.items;
    total = result.total;
    pageSize = result.pageSize;
  }
  if (onlyWithReference) items = items.filter((o) => o.hasTransferReference);

  const link = (target: number) => {
    const query = new URLSearchParams();
    if (status) query.set('status', status);
    if (paymentStatus) query.set('paymentStatus', paymentStatus);
    if (q) query.set('q', q);
    if (target > 1) query.set('page', String(target));
    return `/admin/orders?${query}`;
  };
  const lastPage = Math.max(Math.ceil(total / pageSize), 1);

  return (
    <>
      <h1 className="ui-heading-2">{adminMessages.orders.title}</h1>

      <form method="get" className={styles.filters}>
        <label className={styles.field}>
          <span className={styles.fieldLabel}>{adminMessages.orders.status}</span>
          <select className="ui-control" name="status" defaultValue={status}>
            <option value="">{adminMessages.orders.allStatuses}</option>
            <option value="new">{adminMessages.orders.newGroup}</option>
            {STATUSES.map((s) => (
              <option key={s} value={s}>
                {messages.statuses[s]}
              </option>
            ))}
          </select>
        </label>
        <label className={styles.field}>
          <span className={styles.fieldLabel}>{adminMessages.orders.payment}</span>
          <select className="ui-control" name="paymentStatus" defaultValue={paymentStatus}>
            <option value="">{adminMessages.orders.allPayments}</option>
            <option value="unpaid">{messages.paymentStatuses.unpaid}</option>
            <option value="paid">{messages.paymentStatuses.paid}</option>
          </select>
        </label>
        <label className={styles.field}>
          <span className={styles.fieldLabel}>{adminMessages.orders.search}</span>
          <input className="ui-control" type="search" name="q" defaultValue={q} />
        </label>
        {onlyWithReference && <input type="hidden" name="ref" value="1" />}
        <button className="ui-control" type="submit">
          {adminMessages.orders.filter}
        </button>
      </form>
      {onlyWithReference && <p className={styles.hint}>{adminMessages.orders.hasReference}</p>}

      {items.length === 0 ? (
        <p>{adminMessages.orders.empty}</p>
      ) : (
        <div className={styles.tableWrap}>
          <table className={styles.table}>
            <thead>
              <tr>
                <th>{adminMessages.orders.number}</th>
                <th>{adminMessages.orders.customer}</th>
                <th>{adminMessages.orders.mobile}</th>
                <th>{adminMessages.orders.method}</th>
                <th>{adminMessages.orders.status}</th>
                <th>{adminMessages.orders.payment}</th>
                <th>{adminMessages.orders.total}</th>
                <th>{adminMessages.orders.date}</th>
              </tr>
            </thead>
            <tbody>
              {items.map((o) => (
                <tr key={o.id}>
                  <td className={styles.num}>
                    <Link href={`/admin/orders/${o.id}`}>{formatNumber(o.number)}</Link>
                  </td>
                  <td>{o.fullName}</td>
                  <td className={`${styles.num} ${styles.ltr}`}>{o.mobile}</td>
                  <td>{messages.methods[o.paymentMethod]}</td>
                  <td>
                    <span className={`${styles.badge} ${o.status === 'awaitingPayment' || o.status === 'pending' ? styles.flag : ''}`}>
                      {messages.statuses[o.status]}
                    </span>
                    {o.hasTransferReference && o.status === 'awaitingPayment' && <span className={`${styles.badge} ${styles.ok}`}> {adminMessages.orders.hasReference}</span>}
                  </td>
                  <td>{messages.paymentStatuses[o.paymentStatus]}</td>
                  <td className={styles.num}>{formatNumber(o.total)}</td>
                  <td className={styles.num}>{formatJalaliDate(o.createdAtUtc)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {paging && lastPage > 1 && (
        <nav className={styles.pager} aria-label={messages.pagination}>
          {page > 1 && <Link href={link(page - 1)}>{messages.previousPage}</Link>}
          <span>
            {formatNumber(page)} / {formatNumber(lastPage)}
          </span>
          {page < lastPage && <Link href={link(page + 1)}>{messages.nextPage}</Link>}
        </nav>
      )}
    </>
  );
}
