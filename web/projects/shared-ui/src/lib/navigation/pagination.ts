import { booleanAttribute, ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { UiIcon } from '../icon/icon';
import { formatNumber } from '../core/format';
import { injectUiMessages } from '../core/ui-messages';

export type PageItem = number | 'gap';

/**
 * Pages to render: always first and last, the current page and its neighbours, with "gap" markers.
 * Up to 7 pages are shown in full.   buildPages(5, 12) -> [1, 'gap', 4, 5, 6, 'gap', 12]
 */
export function buildPages(current: number, pageCount: number): PageItem[] {
  if (pageCount <= 7) {
    return Array.from({ length: pageCount }, (_, i) => i + 1);
  }
  const wanted = new Set([1, pageCount, current - 1, current, current + 1]);
  if (current <= 3) {
    [2, 3, 4].forEach((p) => wanted.add(p));
  }
  if (current >= pageCount - 2) {
    [pageCount - 3, pageCount - 2, pageCount - 1].forEach((p) => wanted.add(p));
  }
  const pages = [...wanted].filter((p) => p >= 1 && p <= pageCount).sort((a, b) => a - b);
  const result: PageItem[] = [];
  pages.forEach((p, index) => {
    if (index > 0 && p - pages[index - 1] > 1) {
      result.push('gap');
    }
    result.push(p);
  });
  return result;
}

/**
 * Pagination for listings.
 *  - routed (default): every page is a real link `?page=N` so crawlers can follow it; page 1 drops the
 *    param so it shares the canonical URL. Other query params (filters, sort) are preserved.
 *  - routed=false: buttons that emit pageChange (admin tables).
 */
@Component({
  selector: 'ui-pagination',
  imports: [RouterLink, UiIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (pageCount() > 1) {
      <nav [attr.aria-label]="label() ?? messages.pagination">
        <ul>
          <li>
            @if (page() > 1) {
              @if (routed()) {
                <a class="item nav" rel="prev" [routerLink]="[]" [queryParams]="params(page() - 1)" queryParamsHandling="merge" [attr.aria-label]="messages.previousPage">
                  <ui-icon name="chevron-start" />
                </a>
              } @else {
                <button type="button" class="item nav" [attr.aria-label]="messages.previousPage" (click)="pageChange.emit(page() - 1)">
                  <ui-icon name="chevron-start" />
                </button>
              }
            } @else {
              <span class="item nav disabled" aria-hidden="true"><ui-icon name="chevron-start" /></span>
            }
          </li>
          @for (item of items(); track $index) {
            <li>
              @if (item === 'gap') {
                <span class="gap" aria-hidden="true">…</span>
              } @else if (routed()) {
                <a class="item" [class.current]="item === page()" [routerLink]="[]" [queryParams]="params(item)" queryParamsHandling="merge" [attr.aria-current]="item === page() ? 'page' : null" [attr.aria-label]="messages.page(format(item))">
                  {{ format(item) }}
                </a>
              } @else {
                <button type="button" class="item" [class.current]="item === page()" [attr.aria-current]="item === page() ? 'page' : null" [attr.aria-label]="messages.page(format(item))" (click)="pageChange.emit(item)">
                  {{ format(item) }}
                </button>
              }
            </li>
          }
          <li>
            @if (page() < pageCount()) {
              @if (routed()) {
                <a class="item nav" rel="next" [routerLink]="[]" [queryParams]="params(page() + 1)" queryParamsHandling="merge" [attr.aria-label]="messages.nextPage">
                  <ui-icon name="chevron-end" />
                </a>
              } @else {
                <button type="button" class="item nav" [attr.aria-label]="messages.nextPage" (click)="pageChange.emit(page() + 1)">
                  <ui-icon name="chevron-end" />
                </button>
              }
            } @else {
              <span class="item nav disabled" aria-hidden="true"><ui-icon name="chevron-end" /></span>
            }
          </li>
        </ul>
      </nav>
    }
  `,
  styles: `
    ul {
      display: flex;
      flex-wrap: wrap;
      justify-content: center;
      gap: var(--space-1);
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .item,
    .gap {
      display: inline-grid;
      place-items: center;
      min-inline-size: var(--control-height);
      min-block-size: var(--control-height);
      padding-inline: var(--space-2);
      border: 1px solid transparent;
      border-radius: var(--radius-md);
      background: transparent;
      color: var(--color-text);
      font-variant-numeric: tabular-nums;
      text-decoration: none;
      cursor: pointer;
    }
    .gap {
      color: var(--color-text-subtle);
      cursor: default;
    }
    a.item:hover,
    button.item:hover {
      background: var(--color-surface-muted);
      color: var(--color-text);
    }
    .item.current {
      background: var(--color-primary);
      color: var(--color-primary-contrast);
      font-weight: var(--font-weight-bold);
    }
    .item.current:hover {
      background: var(--color-primary-hover);
      color: var(--color-primary-contrast);
    }
    .item.disabled {
      color: var(--color-text-subtle);
      opacity: 0.5;
      cursor: not-allowed;
    }
  `,
})
export class UiPagination {
  /** Current page, 1-based. */
  readonly page = input.required<number>();
  readonly pageCount = input.required<number>();
  readonly routed = input(true, { transform: booleanAttribute });
  /** Accessible name of the landmark; give each list its own when a page has several. */
  readonly label = input<string>();
  readonly pageChange = output<number>();

  protected readonly messages = injectUiMessages();
  protected readonly items = computed(() => buildPages(this.page(), this.pageCount()));

  protected format(value: number): string {
    return formatNumber(value);
  }

  protected params(target: number): Record<string, number | null> {
    return { page: target === 1 ? null : target };
  }
}
