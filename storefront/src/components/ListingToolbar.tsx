import Link from 'next/link';
import type { FacetsDto } from '@/lib/catalog';
import { formatNumber } from '@/lib/format';
import {
  OPTION_KEYS,
  clearFilters,
  hasFilters,
  listingHref,
  toggleOption,
  withInStock,
  withoutPrice,
  withSort,
  type ListingState,
  type Sort,
} from '@/lib/listing';
import { messages } from '@/lib/messages';
import styles from './ListingToolbar.module.css';

interface Chip {
  label: string;
  href: string;
}

/** One removable chip per active value (labels from the unfiltered category's facets). */
export function activeChips(basePath: string, state: ListingState, allFacets: FacetsDto): Chip[] {
  const chips: Chip[] = [];
  for (const key of OPTION_KEYS) {
    const facet = key === 'brand' ? { name: messages.brand, options: allFacets.brands } : allFacets.attributes.find((a) => a.code === key);
    for (const slug of state.options[key]) {
      const value = facet?.options.find((o) => o.slug === slug)?.value ?? slug;
      chips.push({ label: `${facet?.name ?? key}: ${value}`, href: listingHref(basePath, toggleOption(state, key, slug)) });
    }
  }
  if (state.minPrice !== null || state.maxPrice !== null) {
    const from = state.minPrice === null ? null : formatNumber(state.minPrice);
    const to = state.maxPrice === null ? null : formatNumber(state.maxPrice);
    chips.push({ label: messages.priceChip(from, to), href: listingHref(basePath, withoutPrice(state)) });
  }
  if (state.inStock) chips.push({ label: messages.inStockOnly, href: listingHref(basePath, withInStock(state, false)) });
  return chips;
}

const SORT_OPTIONS: { sort: Sort | null; label: string }[] = [
  { sort: null, label: messages.sortNewest },
  { sort: 'price-asc', label: messages.sortPriceAsc },
  { sort: 'price-desc', label: messages.sortPriceDesc },
];

/** Result count, sort links and active-filter chips. All links are rel="nofollow" (noindex listings). */
export function ListingToolbar({ basePath, state, allFacets, total }: { basePath: string; state: ListingState; allFacets: FacetsDto; total: number }) {
  const chips = activeChips(basePath, state, allFacets);
  return (
    <div className={styles.toolbar}>
      <div className={styles.row}>
        <p className="ui-text-muted">{messages.resultCount(formatNumber(total))}</p>
        <nav aria-label={messages.sort} className={styles.sort}>
          <span className="ui-text-muted">{messages.sort}:</span>
          <ul className={styles.sortList}>
            {SORT_OPTIONS.map((option) => {
              const current = state.sort === option.sort;
              return (
                <li key={option.label}>
                  <Link
                    href={listingHref(basePath, withSort(state, option.sort))}
                    className={`${styles.sortLink} ${current ? styles.current : ''}`}
                    aria-current={current ? 'true' : undefined}
                    rel="nofollow"
                    scroll={false}
                  >
                    {option.label}
                  </Link>
                </li>
              );
            })}
          </ul>
        </nav>
      </div>

      {hasFilters(state) && (
        <div className={styles.chips} aria-label={messages.activeFilters} role="group">
          <ul className={styles.chipList}>
            {chips.map((chip) => (
              <li key={chip.href + chip.label}>
                <Link href={chip.href} className={styles.chip} rel="nofollow" scroll={false} aria-label={messages.removeFilter(chip.label)}>
                  <span>{chip.label}</span>
                  <span aria-hidden="true">×</span>
                </Link>
              </li>
            ))}
            <li>
              <Link href={listingHref(basePath, clearFilters(state))} className={styles.clear} rel="nofollow" scroll={false}>
                {messages.clearFilters}
              </Link>
            </li>
          </ul>
        </div>
      )}
    </div>
  );
}
