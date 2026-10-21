import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { UiIcon } from '../icon/icon';
import { formatDecimal, formatNumber } from '../core/format';
import { injectUiMessages } from '../core/ui-messages';

/**
 * Read-only star rating. Stars are decorative; the whole control exposes one text alternative
 * ("امتیاز ۴٫۵ از ۵") and the numeric value is also shown as text, so meaning never depends on color.
 */
@Component({
  selector: 'ui-rating',
  imports: [UiIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (value() === null || value() === undefined) {
      <span class="none">{{ messages.noRating }}</span>
    } @else {
      <span class="stars" role="img" [attr.aria-label]="ariaLabel()">
        <span class="layer empty" aria-hidden="true">
          @for (i of slots; track i) {
            <ui-icon name="star" filled />
          }
        </span>
        <span class="layer full" aria-hidden="true" [style.inline-size.%]="percent()">
          @for (i of slots; track i) {
            <ui-icon name="star" filled />
          }
        </span>
      </span>
      <span class="value" aria-hidden="true">{{ valueText() }}</span>
      @if (count() !== null && count() !== undefined) {
        <span class="count" aria-hidden="true">({{ countText() }})</span>
      }
    }
  `,
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      gap: var(--space-1);
      font-size: var(--font-size-sm);
    }
    .stars {
      position: relative;
      display: inline-flex;
      --icon-size: 1rem;
    }
    .layer {
      display: inline-flex;
      white-space: nowrap;
    }
    .layer.empty {
      color: var(--color-star-empty);
    }
    .layer.full {
      position: absolute;
      inset-block-start: 0;
      inset-inline-start: 0;
      overflow: hidden;
      color: var(--color-star);
    }
    .value {
      font-weight: var(--font-weight-medium);
    }
    .count,
    .none {
      color: var(--color-text-muted);
    }
  `,
})
export class UiRating {
  readonly value = input<number | null | undefined>();
  readonly count = input<number | null | undefined>();
  readonly max = 5;

  protected readonly messages = injectUiMessages();
  protected readonly slots = [0, 1, 2, 3, 4];

  protected readonly clamped = computed(() => Math.min(Math.max(this.value() ?? 0, 0), this.max));
  protected readonly percent = computed(() => (this.clamped() / this.max) * 100);
  protected readonly valueText = computed(() => formatDecimal(this.clamped()));
  protected readonly countText = computed(() => this.messages.ratingCount(formatNumber(this.count() ?? 0)));
  protected readonly ariaLabel = computed(() => {
    const base = this.messages.rating(this.valueText(), formatNumber(this.max));
    return this.count() == null ? base : `${base}، ${this.countText()}`;
  });
}
