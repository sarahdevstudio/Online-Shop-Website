import Link from 'next/link';
import type { FacetOptionDto, FacetsDto } from '@/lib/catalog';
import { formatNumber } from '@/lib/format';
import {
  OPTION_KEYS,
  listingHref,
  toggleOption,
  withInStock,
  type ListingState,
  type OptionKey,
} from '@/lib/listing';
import { messages } from '@/lib/messages';
import styles from './ListingFilters.module.css';

interface FilterGroup {
  key: OptionKey;
  title: string;
  options: FacetOptionDto[];
}

/**
 * Facet groups in URL order. Counts come from the filtered listing (each facet ignores its own selection); a selected
 * value that has no matches under the other filters is still shown (count 0) so it can be unselected.
 */
export function filterGroups(facets: FacetsDto, allFacets: FacetsDto, state: ListingState): FilterGroup[] {
  const find = (source: FacetsDto, key: OptionKey) =>
    key === 'brand' ? { name: messages.brand, options: source.brands } : source.attributes.find((a) => a.code === key);

  return OPTION_KEYS.flatMap((key) => {
    const all = find(allFacets, key);
    if (!all || all.options.length === 0) return [];
    const counts = new Map((find(facets, key)?.options ?? []).map((o) => [o.slug, o.count]));
    // Keep the category's option order and labels; show options with matches plus selected ones.
    const options = all.options
      .map((o) => ({ ...o, count: counts.get(o.slug) ?? 0 }))
      .filter((o) => o.count > 0 || state.options[key].includes(o.slug));
    return options.length > 0 ? [{ key, title: all.name, options }] : [];
  });
}

interface ListingFiltersProps {
  basePath: string;
  state: ListingState;
  /** Facets of the current (filtered) listing: counts. */
  facets: FacetsDto;
  /** Facets of the unfiltered category: every option and its label. */
  allFacets: FacetsDto;
}

/**
 * Works without JavaScript: every option is a link to the normalized URL of the resulting listing, the price is a
 * GET form (the category page normalizes and 308-redirects its submission). Links are rel="nofollow": filtered
 * listings are noindex.
 */
export function ListingFilters({ basePath, state, facets, allFacets }: ListingFiltersProps) {
  const groups = filterGroups(facets, allFacets, state);
  const range = allFacets.price;

  return (
    <details className={styles.panel}>
      <summary className={styles.summary}>{messages.filters}</summary>
      <div className={styles.body}>
        {groups.map((group) => (
          <fieldset key={group.key} className={styles.group}>
            <legend className={styles.legend}>{group.title}</legend>
            <ul className={styles.options}>
              {group.options.map((option) => {
                const selected = state.options[group.key].includes(option.slug);
                return (
                  <li key={option.slug}>
                    <Link
                      href={listingHref(basePath, toggleOption(state, group.key, option.slug))}
                      className={`${styles.option} ${selected ? styles.selected : ''}`}
                      aria-current={selected ? 'true' : undefined}
                      rel="nofollow"
                      scroll={false}
                    >
                      <span className={styles.box} aria-hidden="true" />
                      <span>{option.value}</span>
                      <span className={styles.count}>({formatNumber(option.count)})</span>
                    </Link>
                  </li>
                );
              })}
            </ul>
          </fieldset>
        ))}

        {range && (
          <form className={styles.group} method="get" action={basePath}>
            <fieldset className={styles.priceFields}>
              <legend className={styles.legend}>{messages.price}</legend>
              {OPTION_KEYS.map(
                (key) =>
                  state.options[key].length > 0 && (
                    <input key={key} type="hidden" name={key} value={state.options[key].join(',')} />
                  ),
              )}
              {state.inStock && <input type="hidden" name="inStock" value="1" />}
              {state.sort && <input type="hidden" name="sort" value={state.sort} />}
              <label className={styles.priceLabel}>
                <span>{messages.priceFrom}</span>
                <input
                  className="ui-control"
                  name="minPrice"
                  inputMode="numeric"
                  autoComplete="off"
                  defaultValue={state.minPrice ?? ''}
                  placeholder={formatNumber(range.min)}
                />
              </label>
              <label className={styles.priceLabel}>
                <span>{messages.priceTo}</span>
                <input
                  className="ui-control"
                  name="maxPrice"
                  inputMode="numeric"
                  autoComplete="off"
                  defaultValue={state.maxPrice ?? ''}
                  placeholder={formatNumber(range.max)}
                />
              </label>
              <button type="submit" className={styles.apply}>
                {messages.applyPrice}
              </button>
            </fieldset>
          </form>
        )}

        <div className={styles.group}>
          <Link
            href={listingHref(basePath, withInStock(state, !state.inStock))}
            className={`${styles.option} ${state.inStock ? styles.selected : ''}`}
            aria-current={state.inStock ? 'true' : undefined}
            rel="nofollow"
            scroll={false}
          >
            <span className={styles.box} aria-hidden="true" />
            <span>{messages.inStockOnly}</span>
          </Link>
        </div>
      </div>
    </details>
  );
}
