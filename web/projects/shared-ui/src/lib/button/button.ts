import { booleanAttribute, ChangeDetectionStrategy, Component, ElementRef, computed, inject, input } from '@angular/core';

export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger';
export type ButtonSize = 'sm' | 'md' | 'lg';

/**
 * Button styles for native <button> and <a> elements, so semantics (submit, href, focus, keyboard)
 * stay native:   <button uiButton variant="primary">افزودن به سبد</button>
 *                <a uiButton variant="secondary" routerLink="/cart">مشاهده سبد</a>
 *
 * `loading` shows a spinner, sets aria-busy and swallows clicks (including Enter/Space and form submit)
 * until it ends. A consumer's own `disabled` attribute is never touched.
 * Icon-only buttons must carry an aria-label.
 */
@Component({
  selector: 'button[uiButton], a[uiButton]',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: {
    '[class]': 'classes()',
    '[attr.aria-busy]': 'loading() ? "true" : null',
  },
  template: `
    @if (loading()) {
      <span class="spinner" aria-hidden="true"></span>
    }
    <ng-content />
  `,
  styleUrl: './button.css',
})
export class UiButton {
  readonly variant = input<ButtonVariant>('primary');
  readonly size = input<ButtonSize>('md');
  readonly loading = input(false, { transform: booleanAttribute });
  readonly block = input(false, { transform: booleanAttribute });
  readonly iconOnly = input(false, { transform: booleanAttribute });

  protected readonly classes = computed(() =>
    [
      'ui-btn',
      `ui-btn--${this.variant()}`,
      `ui-btn--${this.size()}`,
      this.block() ? 'ui-btn--block' : '',
      this.iconOnly() ? 'ui-btn--icon' : '',
      this.loading() ? 'ui-btn--loading' : '',
    ]
      .filter(Boolean)
      .join(' '),
  );

  constructor() {
    // Capture phase runs before any (click) handler the consumer attached to the same element.
    inject<ElementRef<HTMLElement>>(ElementRef).nativeElement.addEventListener(
      'click',
      (event) => {
        if (this.loading()) {
          event.preventDefault();
          event.stopImmediatePropagation();
        }
      },
      { capture: true },
    );
  }
}
