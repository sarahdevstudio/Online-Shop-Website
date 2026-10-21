import Link from 'next/link';
import { Icon } from './Icon';
import { formatNumber } from '@/lib/format';
import { messages } from '@/lib/messages';
import styles from './Pagination.module.css';

type PageItem = number | 'gap';

/** Always first/last, current and neighbours, with gaps. buildPages(5, 12) -> [1, 'gap', 4, 5, 6, 'gap', 12] */
export function buildPages(current: number, pageCount: number): PageItem[] {
  if (pageCount <= 7) return Array.from({ length: pageCount }, (_, i) => i + 1);
  const wanted = new Set([1, pageCount, current - 1, current, current + 1]);
  if (current <= 3) [2, 3, 4].forEach((p) => wanted.add(p));
  if (current >= pageCount - 2) [pageCount - 3, pageCount - 2, pageCount - 1].forEach((p) => wanted.add(p));
  const pages = [...wanted].filter((p) => p >= 1 && p <= pageCount).sort((a, b) => a - b);
  const result: PageItem[] = [];
  pages.forEach((p, index) => {
    if (index > 0 && p - pages[index - 1] > 1) result.push('gap');
    result.push(p);
  });
  return result;
}

interface PaginationProps {
  page: number;
  pageCount: number;
  /** Path of the listing, e.g. /category/shoes. Page 1 drops ?page so it shares the canonical URL. */
  basePath: string;
  /** Other query params (filters, sort) kept on every link. */
  query?: Record<string, string>;
  label?: string;
}

/** Every page is a real link so crawlers can follow it. */
export function Pagination({ page, pageCount, basePath, query = {}, label }: PaginationProps) {
  if (pageCount <= 1) return null;
  const href = (target: number) => {
    const params = new URLSearchParams(query);
    if (target > 1) params.set('page', String(target));
    const qs = params.toString();
    return qs ? `${basePath}?${qs}` : basePath;
  };
  return (
    <nav aria-label={label ?? messages.pagination}>
      <ul className={styles.list}>
        <li>
          {page > 1 ? (
            <Link className={styles.item} rel="prev" href={href(page - 1)} aria-label={messages.previousPage}>
              <Icon name="chevron-start" />
            </Link>
          ) : (
            <span className={`${styles.item} ${styles.disabled}`} aria-hidden="true">
              <Icon name="chevron-start" />
            </span>
          )}
        </li>
        {buildPages(page, pageCount).map((item, index) => (
          <li key={`${item}-${index}`}>
            {item === 'gap' ? (
              <span className={styles.gap} aria-hidden="true">…</span>
            ) : (
              <Link
                className={`${styles.item} ${item === page ? styles.current : ''}`}
                href={href(item)}
                aria-current={item === page ? 'page' : undefined}
                aria-label={messages.page(formatNumber(item))}
              >
                {formatNumber(item)}
              </Link>
            )}
          </li>
        ))}
        <li>
          {page < pageCount ? (
            <Link className={styles.item} rel="next" href={href(page + 1)} aria-label={messages.nextPage}>
              <Icon name="chevron-end" />
            </Link>
          ) : (
            <span className={`${styles.item} ${styles.disabled}`} aria-hidden="true">
              <Icon name="chevron-end" />
            </span>
          )}
        </li>
      </ul>
    </nav>
  );
}
