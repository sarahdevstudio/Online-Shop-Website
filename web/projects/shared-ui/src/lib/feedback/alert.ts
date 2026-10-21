import { booleanAttribute, ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { UiIcon } from '../icon/icon';
import { IconName } from '../icon/icons';
import { injectUiMessages } from '../core/ui-messages';

export type AlertVariant = 'info' | 'success' | 'warning' | 'danger';

const ICON_BY_VARIANT: Record<AlertVariant, IconName> = {
  info: 'info',
  success: 'check',
  warning: 'alert',
  danger: 'alert',
};

/** Inline message. danger/warning interrupt screen readers (role=alert); info/success are polite (role=status). */
@Component({
  selector: 'ui-alert',
  imports: [UiIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[class]': '"v-" + variant()',
    '[attr.role]': 'role()',
  },
  template: `
    <ui-icon class="icon" [name]="icon()" />
    <div class="body">
      @if (heading()) {
        <strong class="heading">{{ heading() }}</strong>
      }
      <div><ng-content /></div>
    </div>
    @if (dismissible()) {
      <button type="button" class="dismiss" [attr.aria-label]="messages.close" (click)="dismissed.emit()">
        <ui-icon name="x" />
      </button>
    }
  `,
  styles: `
    :host {
      display: flex;
      gap: var(--space-3);
      align-items: flex-start;
      padding: var(--space-3) var(--space-4);
      border-radius: var(--radius-md);
      border: 1px solid currentColor;
      font-size: var(--font-size-sm);
      line-height: var(--line-height-snug);
    }
    :host(.v-info) { background: var(--color-info-soft); color: var(--color-info); }
    :host(.v-success) { background: var(--color-success-soft); color: var(--color-success); }
    :host(.v-warning) { background: var(--color-warning-soft); color: var(--color-warning); }
    :host(.v-danger) { background: var(--color-danger-soft); color: var(--color-danger); }
    .icon { margin-block-start: 0.2em; }
    .body { flex: 1; min-inline-size: 0; }
    .heading { display: block; margin-block-end: var(--space-1); }
    .dismiss {
      flex: none;
      display: grid;
      place-items: center;
      inline-size: var(--control-height-sm);
      block-size: var(--control-height-sm);
      margin: calc(var(--space-2) * -1);
      border: 0;
      border-radius: var(--radius-sm);
      background: transparent;
      color: inherit;
      cursor: pointer;
    }
    .dismiss:hover { background: rgb(0 0 0 / 0.06); }
  `,
})
export class UiAlert {
  readonly variant = input<AlertVariant>('info');
  readonly heading = input<string>();
  readonly dismissible = input(false, { transform: booleanAttribute });
  readonly dismissed = output<void>();

  protected readonly messages = injectUiMessages();
  protected readonly icon = computed(() => ICON_BY_VARIANT[this.variant()]);
  protected readonly role = computed(() => (this.variant() === 'danger' || this.variant() === 'warning' ? 'alert' : 'status'));
}
