import Link from 'next/link';
import { Alert } from '@/components/Alert';
import { Form, SubmitButton, TextField } from '@/components/form';
import { adminGet } from '@/lib/admin/api';
import { adminMessages } from '@/lib/admin/messages';
import type { PagedList, RedirectRow, RouteAuditEntry } from '@/lib/admin/types';
import { formatJalaliDate, formatNumber } from '@/lib/format';
import { messages } from '@/lib/messages';
import type { SearchParams } from '@/lib/paging';
import { ReleaseForm } from './ReleaseForm';
import styles from '../../admin.module.css';

const t = adminMessages.routes;
const first = (value: string | string[] | undefined) => (Array.isArray(value) ? value[0] : value) ?? '';

/** Where the admin edits each kind of target (the id of an info page is not its admin address, so those link to the list). */
const EDIT_LINKS: Record<string, (id: number) => string> = {
  product: (id) => `/admin/products/${id}`,
  blogPost: (id) => `/admin/blog/${id}`,
  category: () => '/admin/categories',
  contentPage: () => '/admin/content',
};

export default async function RedirectsPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const sp = await searchParams;
  const q = first(sp.q).trim();
  const page = Math.max(Number.parseInt(first(sp.page), 10) || 1, 1);
  const released = first(sp.released) === '1';

  const query = new URLSearchParams({ page: String(page) });
  if (q) query.set('q', q);
  const [list, audit] = await Promise.all([
    adminGet<PagedList<RedirectRow>>(`/admin/routes/redirects?${query}`),
    adminGet<PagedList<RouteAuditEntry>>('/admin/routes/audit'),
  ]);
  if (!list) throw new Error('Redirects are unavailable.');
  const lastPage = Math.max(Math.ceil(list.total / list.pageSize), 1);
  const link = (target: number) => {
    const next = new URLSearchParams();
    if (q) next.set('q', q);
    if (target > 1) next.set('page', String(target));
    const text = next.toString();
    return text ? `/admin/redirects?${text}` : '/admin/redirects';
  };
  const back = link(page);

  return (
    <>
      <h1 className="ui-heading-2">{t.title}</h1>
      <p className={styles.hint}>{t.hint}</p>
      {released && <Alert variant="success">{t.released}</Alert>}

      <Form method="get" inline>
        <TextField name="q" label={t.search} type="search" defaultValue={q} autoComplete="off" />
        <SubmitButton variant="secondary">{t.filter}</SubmitButton>
      </Form>

      {list.items.length === 0 ? (
        <p>{t.empty}</p>
      ) : (
        <div className={styles.tableWrap} tabIndex={0} role="region" aria-label={adminMessages.scrollableTable}>
          <table className={styles.table}>
            <caption className="ui-visually-hidden">{t.title}</caption>
            <thead>
              <tr>
                <th scope="col">{t.oldPath}</th>
                <th scope="col">{t.goesTo}</th>
                <th scope="col">{t.target}</th>
                <th scope="col">{t.created}</th>
                <th scope="col">{t.release}</th>
              </tr>
            </thead>
            <tbody>
              {list.items.map((row) => (
                <tr key={row.id}>
                  <td className={styles.ltr}>{row.path}</td>
                  <td className={styles.ltr}>{row.isGone || !row.currentPath ? <span className={`${styles.badge} ${styles.flag}`}>{t.gone}</span> : row.currentPath}</td>
                  <td>
                    {t.types[row.targetType] ?? row.targetType}
                    {row.targetName && (
                      <>
                        {': '}
                        {row.isGone ? row.targetName : <Link href={(EDIT_LINKS[row.targetType] ?? (() => '/admin'))(row.targetId)}>{row.targetName}</Link>}
                      </>
                    )}
                  </td>
                  <td className={styles.num}>{formatJalaliDate(row.createdAtUtc)}</td>
                  <td>
                    <ReleaseForm id={row.id} path={row.path} back={back} />
                  </td>
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

      <section className={styles.panel} aria-labelledby="route-audit">
        <h2 id="route-audit" className="ui-heading-4">
          {t.auditTitle}
        </h2>
        {(audit?.items ?? []).length === 0 ? (
          <p className={styles.muted}>{t.auditEmpty}</p>
        ) : (
          <ul>
            {(audit?.items ?? []).map((entry, i) => (
              <li key={i}>
                {formatJalaliDate(entry.atUtc)} — <bdi dir="ltr">{entry.adminUsername ?? adminMessages.none}</bdi> —{' '}
                {t.types[entry.targetType] ?? entry.targetType}
                {entry.targetName ? `: ${entry.targetName}` : ''} — {t.actions[entry.action] ?? entry.action}
                {(entry.oldPath || entry.newPath) && (
                  <>
                    {' — '}
                    <bdi dir="ltr">
                      {entry.oldPath ?? adminMessages.none} → {entry.newPath ?? adminMessages.none}
                    </bdi>
                  </>
                )}
              </li>
            ))}
          </ul>
        )}
      </section>
    </>
  );
}
