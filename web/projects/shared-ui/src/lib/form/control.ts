import { Directive, inject } from '@angular/core';
import { UiField } from './field';

/**
 * Styles a native input/select/textarea and wires it to the surrounding <ui-field>
 * (id for the label, aria-describedby, aria-invalid). Works standalone too, without a field.
 */
@Directive({
  selector: 'input[uiControl], select[uiControl], textarea[uiControl]',
  host: {
    class: 'ui-control',
    '[attr.id]': 'field?.controlId ?? null',
    '[attr.aria-describedby]': 'field?.describedBy() ?? null',
    '[attr.aria-invalid]': 'field?.invalid() ? "true" : null',
  },
})
export class UiControl {
  protected readonly field = inject(UiField, { optional: true });
}

/** Checkbox / radio look with native semantics:  <label class="ui-check-row"><input uiCheck type="checkbox" /> متن</label> */
@Directive({
  selector: 'input[uiCheck]',
  host: { class: 'ui-check' },
})
export class UiCheck {}
