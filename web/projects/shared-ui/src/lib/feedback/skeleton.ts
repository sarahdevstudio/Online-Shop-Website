import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Loading placeholder. Purely visual (aria-hidden): put aria-busy="true" on the loading region and
 * a <ui-spinner> or live text for assistive technology.
 */
@Component({
  selector: 'ui-skeleton',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    'aria-hidden': 'true',
    '[class.circle]': 'shape() === "circle"',
    '[style.inline-size]': 'width()',
    '[style.block-size]': 'height()',
  },
  template: '',
  styles: `
    :host {
      display: block;
      inline-size: 100%;
      block-size: 1em;
      border-radius: var(--radius-sm);
      background: linear-gradient(
        90deg,
        var(--color-surface-sunken) 25%,
        var(--color-surface-muted) 37%,
        var(--color-surface-sunken) 63%
      );
      background-size: 400% 100%;
      animation: shimmer 1.4s ease infinite;
    }
    :host(.circle) {
      border-radius: var(--radius-full);
    }
    @keyframes shimmer {
      0% {
        background-position: 100% 50%;
      }
      100% {
        background-position: 0 50%;
      }
    }
  `,
})
export class UiSkeleton {
  readonly shape = input<'line' | 'rect' | 'circle'>('line');
  readonly width = input<string>();
  readonly height = input<string>();
}
