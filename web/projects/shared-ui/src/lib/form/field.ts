import { booleanAttribute, ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { UiIcon } from '../icon/icon';
import { injectUiMessages } from '../core/ui-messages';

let nextFieldId = 0;

/**
 * Label + hint + error wrapper around one native control marked with `uiControl`:
 *
 *   <ui-field label="ایمیل" [error]="emailError()" hint="برای ورود استفاده می‌شود" required>
 *     <input uiControl type="email" formControlName="email" autocomplete="email" />
 *   </ui-field>
 *
 * The control gets its id, aria-describedby and aria-invalid from the field, so every input is labelled
 * and its error is announced. Pass `error` only after the user has interacted (touched/submitted).
 */
@Component({
  selector: 'ui-field',
  imports: [UiIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <label class="label" [attr.for]="controlId">
      {{ label() }}
      @if (required()) {
        <span class="required" aria-hidden="true">*</span>
        <span class="ui-visually-hidden">({{ messages.required }})</span>
      }
    </label>
    @if (hint()) {
      <p class="hint" [id]="hintId">{{ hint() }}</p>
    }
    <ng-content />
    @if (error()) {
      <p class="error" [id]="errorId" role="alert">
        <ui-icon name="alert" style="--icon-size: 1rem" />
        <span>{{ error() }}</span>
      </p>
    }
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      gap: var(--space-1);
    }
    .label {
      font-weight: var(--font-weight-medium);
      font-size: var(--font-size-sm);
    }
    .required {
      color: var(--color-danger);
      margin-inline-start: var(--space-1);
    }
    .hint {
      margin: 0;
      font-size: var(--font-size-sm);
      color: var(--color-text-muted);
    }
    .error {
      display: flex;
      align-items: flex-start;
      gap: var(--space-1);
      margin: 0;
      font-size: var(--font-size-sm);
      color: var(--color-danger);
    }
    .error ui-icon {
      margin-block-start: 0.35em;
    }
  `,
})
export class UiField {
  readonly label = input.required<string>();
  readonly hint = input<string>();
  readonly error = input<string | null | undefined>();
  readonly required = input(false, { transform: booleanAttribute });

  protected readonly messages = injectUiMessages();

  private readonly uid = ++nextFieldId;
  readonly controlId = `ui-field-${this.uid}`;
  readonly hintId = `ui-field-${this.uid}-hint`;
  readonly errorId = `ui-field-${this.uid}-error`;

  /** Space-separated ids for aria-describedby, or null when the field has neither hint nor error. */
  readonly describedBy = computed(() => {
    const ids = [this.hint() ? this.hintId : '', this.error() ? this.errorId : ''].filter(Boolean);
    return ids.length ? ids.join(' ') : null;
  });

  readonly invalid = computed(() => !!this.error());
}
