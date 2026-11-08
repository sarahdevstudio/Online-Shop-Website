import Link from 'next/link';
import { cookies } from 'next/headers';
import { Button } from '@/components/Button';
import { Form, SubmitButton } from '@/components/form';
import { getProduct } from '@/lib/catalog';
import { buildComparison, COMPARE_COOKIE, COMPARE_MAX, parseCompare } from '@/lib/compare';
import { compareMessages as t } from '@/lib/compare-messages';
import { formatNumber } from '@/lib/format';
import { messages } from '@/lib/messages';
import { pageMetadata } from '@/lib/seo';
import type { SearchParams } from '@/lib/paging';
import { clearCompareAction, toggleCompareAction } from './actions';
import styles from './compare.module.css';

/** A personal list in a cookie: never indexed, and not worth a canonical. */
export async function generateMetadata() {
  return pageMetadata({ title: t.title, description: t.description, path: '/compare', noindex: true, canonical: false });
}

export default async function ComparePage({ searchParams }: { searchParams: Promise<SearchParams> }) {
  const query = await searchParams;
  const onlyDifferences = query.diff === '1';
  const slugs = parseCompare((await cookies()).get(COMPARE_COOKIE)?.value);
  // A product that is no longer public simply drops out (the cookie is tidied the next time the list changes).
  const products = (await Promise.all(slugs.map((slug) => getProduct(slug)))).filter((p): p is NonNullable<typeof p> => p !== null);

  if (products.length === 0) {
    return (
      <div className="ui-container ui-section ui-stack">
        <h1 className="ui-heading-2">{t.title}</h1>
        <p>
          <strong>{t.emptyTitle}</strong>
        </p>
        <p>{t.emptyText(formatNumber(COMPARE_MAX))}</p>
        <p>
          <Link href="/">{messages.home}</Link>
        </p>
      </div>
    );
  }

  const { columns, rows } = buildComparison(products);
  const shown = onlyDifferences ? rows.filter((row) => row.differs) : rows;

  return (
    <div className="ui-container ui-section ui-stack">
      <h1 className="ui-heading-2">{t.title}</h1>
      <div className={styles.tools}>
        <Link href={onlyDifferences ? '/compare' : '/compare?diff=1'}>
          {onlyDifferences ? t.allRows : t.onlyDifferences}
        </Link>
        <Form action={clearCompareAction} inline>
          <SubmitButton variant="secondary">{t.clear}</SubmitButton>
        </Form>
      </div>
      {columns.length < 2 && <p className="ui-text-muted">{t.oneMore}</p>}

      <div className={styles.scroll} tabIndex={0} role="region" aria-label={t.scrollRegion}>
        <table className={styles.table}>
          <caption className="ui-visually-hidden">{t.tableCaption}</caption>
          <thead>
            <tr>
              <th scope="col">
                <span className="ui-visually-hidden">{t.tableCaption}</span>
              </th>
              {columns.map((column) => (
                <th key={column.slug} scope="col">
                  <div className={styles.product}>
                    {/* eslint-disable-next-line @next/next/no-img-element */}
                    {column.image && <img src={column.image.url} alt="" width={96} height={96} loading="lazy" />}
                    <Link href={column.path}>{column.name}</Link>
                    <Form action={toggleCompareAction} inline>
                      <input type="hidden" name="slug" value={column.slug} />
                      <input type="hidden" name="returnTo" value={onlyDifferences ? '/compare?diff=1' : '/compare'} />
                      <Button type="submit" variant="secondary" size="sm" aria-label={t.removeNamed(column.name)}>
                        {t.remove}
                      </Button>
                    </Form>
                  </div>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {shown.length === 0 ? (
              <tr>
                <th scope="row">{t.onlyDifferences}</th>
                <td colSpan={columns.length}>{t.noDifferences}</td>
              </tr>
            ) : (
              shown.map((row) => (
                <tr key={row.label} className={row.differs ? styles.diff : undefined}>
                  <th scope="row">
                    {row.label}
                    {row.differs && columns.length > 1 && <span className={styles.diffMark}>{t.differs}</span>}
                  </th>
                  {row.values.map((value, i) => (
                    <td key={columns[i].slug}>{value}</td>
                  ))}
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
