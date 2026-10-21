import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { injectUiMessages } from '../core/ui-messages';

@Component({
  selector: 'ui-spinner',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { role: 'status' },
  template: `
    <span class="ring" aria-hidden="true"></span>
    <span class="ui-visually-hidden">{{ label() ?? messages.loading }}</span>
  `,
  styles: `
    :host {
      display: inline-flex;
      color: var(--color-primary);
    }
    .ring {
      inline-size: var(--spinner-size, 1.75rem);
      block-size: var(--spinner-size, 1.75rem);
      border: 3px solid var(--color-border);
      border-block-start-color: currentColor;
      border-radius: var(--radius-full);
      animation: spin 0.8s linear infinite;
    }
    @keyframes spin {
      to {
        transform: rotate(360deg);
      }
    }
  `,
})
export class UiSpinner {
  readonly label = input<string>();
  protected readonly messages = injectUiMessages();
}
