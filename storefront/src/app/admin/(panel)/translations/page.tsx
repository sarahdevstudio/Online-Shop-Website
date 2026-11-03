import Link from 'next/link';
import { Form, SelectField, SubmitButton, CheckField, TextField } from '@/components/form';
import { adminGet } from '@/lib/admin/api';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminTranslation, PagedList, TranslationArea } from '@/lib/admin/types';
import { formatJalaliDate, formatNumber } from '@/lib/format';
import { messages } from '@/lib/messages';
import type { SearchParams } from '@/lib/paging';
import styles from '../../admin.module.css';

const t = adminMessages.translations;
const first = (value: string | string[] | undefined) => (Array.isArray(value) ? value[0] : value) ?? '';

export default async function TranslationsPage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const sp = await searchParams;
  const q = first(sp.q).trim();
  const area = first(sp.area).trim();
  const edited = first(sp.edited) === '1';
  const page = Math.max(Number.parseInt(first(sp.page), 10) || 1, 1);

  const query = new URLSearchParams({ page: String(page) });
  if (q) query.set('q', q);
  if (area) query.set('area', area);
  if (edited) query.set('edited', 'true');
  const [list, areas] = await Promise.all([adminGet<PagedList<AdminTranslation>>(`/admin/translations?${query}`), adminGet<TranslationArea[]>('/admin/translations/areas')]);
  if (!list) throw new Error('Translations are unavailable.');
  const lastPage = Math.max(Math.ceil(list.total / list.pageSize), 1);
  const link = (target: number) => {
    const next = new URLSearchParams();
    if (q) next.set('q', q);
    if (area) next.set('area', area);
    if (edited) next.set('edited', '1');
    if (target > 1) next.set('page', String(target));
    return `/admin/translations?${next}`;
  };

  return (
    <>
      <h1 className="ui-heading-2">{t.title}</h1>
      <p className={styles.hint}>{t.hint}</p>

      <Form method="get" inline>
        <TextField name="q" label={t.search} type="search" defaultValue={q} autoComplete="off" />
        <SelectField name="area" label={t.area} defaultValue={area}>
          <option value="">{t.allAreas}</option>
          {(areas ?? []).map((a) => (
            <option key={a.area} value={a.area}>
              {a.area} ({formatNumber(a.count)})
            </option>
          ))}
        </SelectField>
        <CheckField name="edited" label={t.editedOnly} value="1" defaultChecked={edited} />
        <SubmitButton variant="secondary">{t.filter}</SubmitButton>
      </Form>

      {list.items.length === 0 ? (
        <p>{t.empty}</p>
      ) : (
        <div className={styles.tableWrap} tabIndex={0} role="region" aria-label={adminMessages.scrollableTable}>
          <table className={styles.table}>
            <thead>
              <tr>
                <th>{t.key}</th>
                <th>{t.text}</th>
                <th>{t.lastEdit}</th>
                <th>{t.by}</th>
              </tr>
            </thead>
            <tbody>
              {list.items.map((row) => (
                <tr key={row.id}>
                  <td>
                    <Link href={`/admin/translations/${row.id}`}>
                      <bdi dir="ltr">{row.key}</bdi>
                    </Link>{' '}
                    {row.isEdited && <span className={`${styles.badge} ${styles.flag}`}>{t.edited}</span>}{' '}
                    {!row.inUse && <span className={styles.badge}>{t.unused}</span>}
                  </td>
                  <td>{row.text}</td>
                  <td className={styles.num}>{row.isEdited ? formatJalaliDate(row.updatedAtUtc) : adminMessages.none}</td>
                  <td className={styles.ltr}>{row.updatedBy ?? adminMessages.none}</td>
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
