import { booleanAttribute, ChangeDetectionStrategy, Component, computed, input, model } from '@angular/core';
import { UiIcon } from '../icon/icon';
import { formatNumber, parseLocalizedInteger } from '../core/format';
import { injectUiMessages } from '../core/ui-messages';

/**
 * Quantity control for cart lines and the product page:  <ui-quantity-stepper [(value)]="qty" [max]="stock" />
 * Accepts Persian/Arabic/Latin digits, clamps to [min, max]. `max` is a UX hint only: the server always
 * re-validates quantity and stock.
 */
@Component({
  selector: 'ui-quantity-stepper',
  imports: [UiIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="group" role="group" [attr.aria-label]="messages.quantity">
      <button
        type="button"
        class="step"
        [attr.aria-label]="messages.decreaseQuantity"
        [disabled]="disabled() || value() <= min()"
        (click)="set(value() - 1)"
      >
        <ui-icon name="minus" />
      </button>
      <input
        class="input"
        type="text"
        inputmode="numeric"
        autocomplete="off"
        role="spinbutton"
        [attr.aria-label]="messages.quantity"
        [attr.aria-valuemin]="min()"
        [attr.aria-valuemax]="max() ?? null"
        [attr.aria-valuenow]="value()"
        [value]="display()"
        [disabled]="disabled()"
        (change)="onInput($event)"
        (keydown.arrowup)="set(value() + 1); $event.preventDefault()"
        (keydown.arrowdown)="set(value() - 1); $event.preventDefault()"
      />
      <button
        type="button"
        class="step"
        [attr.aria-label]="messages.increaseQuantity"
        [disabled]="disabled() || (max() !== null && max() !== undefined && value() >= max()!)"
        (click)="set(value() + 1)"
      >
        <ui-icon name="plus" />
      </button>
    </div>
  `,
  styles: `
    :host {
      display: inline-block;
    }
    .group {
      display: inline-flex;
      align-items: stretch;
      border: 1px solid var(--color-border-control);
      border-radius: var(--radius-md);
      background: var(--color-surface);
      overflow: hidden;
    }
    .step {
      display: grid;
      place-items: center;
      inline-size: var(--control-height);
      min-block-size: calc(var(--control-height) - 2px);
      border: 0;
      background: transparent;
      color: var(--color-text);
      cursor: pointer;
    }
    .step:hover:not(:disabled) {
      background: var(--color-surface-muted);
    }
    .step:disabled {
      color: var(--color-text-subtle);
      cursor: not-allowed;
    }
    .input {
      inline-size: 3rem;
      border: 0;
      border-inline: 1px solid var(--color-border);
      background: transparent;
      text-align: center;
      font-weight: var(--font-weight-medium);
      font-variant-numeric: tabular-nums;
    }
    .input:focus-visible {
      border-radius: 0;
    }
  `,
})
export class UiQuantityStepper {
  readonly value = model(1);
  readonly min = input(1);
  readonly max = input<number | null | undefined>();
  readonly disabled = input(false, { transform: booleanAttribute });

  protected readonly messages = injectUiMessages();
  protected readonly display = computed(() => formatNumber(this.value()));

  protected set(next: number): void {
    const max = this.max();
    const upper = max == null ? Number.MAX_SAFE_INTEGER : Math.max(max, this.min());
    this.value.set(Math.min(Math.max(next, this.min()), upper));
  }

  protected onInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const parsed = parseLocalizedInteger(input.value);
    this.set(parsed ?? this.value());
    input.value = this.display(); // normalise what the user typed, even when the value did not change
  }
}
