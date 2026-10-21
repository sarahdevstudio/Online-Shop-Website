import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export type BadgeVariant = 'neutral' | 'primary' | 'sale' | 'success' | 'warning' | 'danger';

@Component({
  selector: 'ui-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { '[class]': '"v-" + variant()' },
  template: '<ng-content />',
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      padding-block: 0.1rem;
      padding-inline: var(--space-2);
      border-radius: var(--radius-full);
      font-size: var(--font-size-xs);
      font-weight: var(--font-weight-medium);
      line-height: var(--line-height-snug);
      white-space: nowrap;
    }
    :host(.v-neutral) { background: var(--color-surface-sunken); color: var(--color-text); }
    :host(.v-primary) { background: var(--color-primary-soft); color: var(--color-primary); }
    :host(.v-sale) { background: var(--color-sale); color: var(--color-sale-contrast); }
    :host(.v-success) { background: var(--color-success-soft); color: var(--color-success); }
    :host(.v-warning) { background: var(--color-warning-soft); color: var(--color-warning); }
    :host(.v-danger) { background: var(--color-danger-soft); color: var(--color-danger); }
  `,
})
export class UiBadge {
  readonly variant = input<BadgeVariant>('neutral');
}
