import Link from 'next/link';
import { adminGet } from '@/lib/admin/api';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminProductListItem, PagedList } from '@/lib/admin/types';
import { formatJalaliDate, formatNumber } from '@/lib/format';
import { messages } from '@/lib/messages';
import type { SearchParams } from '@/lib/paging';
import styles from '../../admin.module.css';

const first = (value: string | string[] | undefined) => (Array.isArray(value) ? value[0] : value) ?? '';
const STATUSES = ['draft', 'published', 'archived'];

export default async function ProductsPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const sp = await searchParams;
  const q = first(sp.q).trim();
  const status = STATUSES.includes(first(sp.status)) ? first(sp.status) : '';
  const page = Math.max(Number.parseInt(first(sp.page), 10) || 1, 1);
  const query = new URLSearchParams({ page: String(page) });
  if (q) query.set('q', q);
  if (status) query.set('status', status);
  const list = await adminGet<PagedList<AdminProductListItem>>(`/admin/products?${query}`);
  if (!list) throw new Error('Products are unavailable.');
  const lastPage = Math.max(Math.ceil(list.total / list.pageSize), 1);
  const link = (target: number) => {
    const next = new URLSearchParams();
    if (q) next.set('q', q);
    if (status) next.set('status', status);
    if (target > 1) next.set('page', String(target));
    return `/admin/products?${next}`;
  };

  return (
    <>
      <div className={styles.row}>
        <h1 className="ui-heading-2">{adminMessages.products.title}</h1>
        <Link href="/admin/products/new" className="ui-control" style={{ inlineSize: 'auto', textDecoration: 'none' }}>
          {adminMessages.products.new}
        </Link>
      </div>

      <form method="get" className={styles.filters}>
        <label className={styles.field}>
          <span className={styles.fieldLabel}>{adminMessages.products.search}</span>
          <input className="ui-control" type="search" name="q" defaultValue={q} />
        </label>
        <label className={styles.field}>
          <span className={styles.fieldLabel}>{adminMessages.products.status}</span>
          <select className="ui-control" name="status" defaultValue={status}>
            <option value="">{adminMessages.orders.allStatuses}</option>
            {STATUSES.map((s) => (
              <option key={s} value={s}>
                {adminMessages.products.statuses[s]}
              </option>
            ))}
          </select>
        </label>
        <button className="ui-control" type="submit">
          {adminMessages.orders.filter}
        </button>
      </form>

      {list.items.length === 0 ? (
        <p>{adminMessages.products.empty}</p>
      ) : (
        <div className={styles.tableWrap}>
          <table className={styles.table}>
            <thead>
              <tr>
                <th>{adminMessages.products.name}</th>
                <th>{adminMessages.products.category}</th>
                <th>{adminMessages.products.brand}</th>
                <th>{adminMessages.products.status}</th>
                <th>{adminMessages.products.variants}</th>
                <th>{adminMessages.products.price}</th>
                <th>{adminMessages.products.stock}</th>
                <th>{adminMessages.orders.date}</th>
              </tr>
            </thead>
            <tbody>
              {list.items.map((p) => (
                <tr key={p.id}>
                  <td>
                    <Link href={`/admin/products/${p.id}`}>{p.name}</Link>
                  </td>
                  <td>{p.category}</td>
                  <td>{p.brand ?? adminMessages.none}</td>
                  <td>
                    <span className={styles.badge}>{adminMessages.products.statuses[p.status]}</span>
                  </td>
                  <td className={styles.num}>{formatNumber(p.variantCount)}</td>
                  <td className={styles.num}>{p.minPrice === null ? adminMessages.none : formatNumber(p.minPrice)}</td>
                  <td className={styles.num}>{formatNumber(p.totalStock)}</td>
                  <td className={styles.num}>{formatJalaliDate(p.updatedAtUtc)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {lastPage > 1 && (
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
