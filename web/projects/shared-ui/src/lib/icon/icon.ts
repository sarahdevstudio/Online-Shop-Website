import { booleanAttribute, ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { DIRECTIONAL_ICONS, ICONS, IconName } from './icons';

/**
 * Inline SVG icon. Decorative by default (aria-hidden); pass `label` when the icon alone carries meaning.
 * Size follows `--icon-size` (default 1.25rem) so parents can scale icons with CSS.
 */
@Component({
  selector: 'ui-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[class.ui-icon--directional]': 'directional()',
  },
  template: `
    <svg
      viewBox="0 0 24 24"
      [attr.fill]="filled() ? 'currentColor' : 'none'"
      stroke="currentColor"
      stroke-width="1.75"
      stroke-linecap="round"
      stroke-linejoin="round"
      [attr.role]="label() ? 'img' : null"
      [attr.aria-label]="label() || null"
      [attr.aria-hidden]="label() ? null : 'true'"
      focusable="false"
    >
      @for (d of paths(); track $index) {
        <path [attr.d]="d" />
      }
    </svg>
  `,
  styles: `
    :host {
      display: inline-flex;
      flex: none;
      inline-size: var(--icon-size, 1.25rem);
      block-size: var(--icon-size, 1.25rem);
    }
    svg {
      inline-size: 100%;
      block-size: 100%;
    }
  `,
})
export class UiIcon {
  readonly name = input.required<IconName>();
  readonly label = input<string>();
  readonly filled = input(false, { transform: booleanAttribute });

  protected readonly paths = computed(() => ICONS[this.name()]);
  protected readonly directional = computed(() => DIRECTIONAL_ICONS.has(this.name()));
}
