import { booleanAttribute, ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { UiBadge } from '../feedback/badge';
import { discountFraction, formatNumber, formatPercent } from '../core/format';
import { injectUiMessages } from '../core/ui-messages';

/**
 * Price display. The amount is always supplied by the server (never computed from user input).
 * With `compareAt` > `amount` it shows the old price struck through and a discount badge.
 * Pass `amount = null` for "call for price" products.
 */
@Component({
  selector: 'ui-price',
  imports: [UiBadge],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class.lg]': 'size() === "lg"' },
  template: `
    @if (amount() === null) {
      <span class="call">{{ messages.callForPrice }}</span>
    } @else {
      <span class="row">
        <span class="current">
          <span class="amount">{{ formattedAmount() }}</span>
          <span class="unit">{{ unit() ?? messages.currencyUnit }}</span>
        </span>
        @if (discount() > 0 && showBadge()) {
          <ui-badge variant="sale">{{ percentText() }}</ui-badge>
        }
      </span>
      @if (discount() > 0) {
        <span class="ui-visually-hidden">{{ messages.previousPrice }}:</span>
        <del class="compare">{{ formattedCompareAt() }}</del>
      }
    }
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: var(--space-1);
      font-variant-numeric: tabular-nums;
    }
    .row {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: var(--space-2);
    }
    .current {
      display: inline-flex;
      align-items: baseline;
      gap: var(--space-1);
    }
    .amount {
      font-size: var(--font-size-lg);
      font-weight: var(--font-weight-bold);
    }
    :host(.lg) .amount {
      font-size: var(--font-size-3xl);
    }
    .unit {
      font-size: var(--font-size-xs);
      color: var(--color-text-muted);
    }
    .compare {
      font-size: var(--font-size-sm);
      color: var(--color-text-subtle);
      text-decoration-thickness: 1px;
    }
    .call {
      font-weight: var(--font-weight-medium);
      color: var(--color-text-muted);
    }
  `,
})
export class UiPrice {
  readonly amount = input.required<number | null>();
  readonly compareAt = input<number | null | undefined>();
  readonly unit = input<string>();
  readonly size = input<'md' | 'lg'>('md');
  /** Cards show the badge on the image instead and turn this off. */
  readonly showBadge = input(true, { transform: booleanAttribute });

  protected readonly messages = injectUiMessages();

  protected readonly discount = computed(() => {
    const amount = this.amount();
    return amount === null ? 0 : discountFraction(amount, this.compareAt());
  });
  protected readonly formattedAmount = computed(() => formatNumber(this.amount() ?? 0));
  protected readonly formattedCompareAt = computed(() => formatNumber(this.compareAt() ?? 0));
  protected readonly percentText = computed(() => this.messages.discountOff(formatPercent(this.discount())));
}
