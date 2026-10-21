import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { UiIcon } from '../icon/icon';
import { injectUiMessages } from '../core/ui-messages';
import { UiDialogBase } from './dialog-base';

/**
 * Centered dialog for confirmations and short forms.
 *
 *   <ui-modal [(open)]="confirmOpen" heading="حذف آدرس">
 *     متن...
 *     <div uiDialogFooter> <button uiButton ...>تأیید</button> </div>
 *   </ui-modal>
 */
@Component({
  selector: 'ui-modal',
  imports: [UiIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dialog
      #dialog
      class="dialog"
      [class.wide]="size() === 'lg'"
      [attr.aria-labelledby]="titleId"
      (close)="onClosed()"
      (click)="onBackdropClick($event)"
    >
      <div class="panel">
        <header class="header">
          <h2 class="title" [id]="titleId">{{ heading() }}</h2>
          <button type="button" class="close" [attr.aria-label]="messages.close" (click)="open.set(false)">
            <ui-icon name="x" />
          </button>
        </header>
        <div class="body"><ng-content /></div>
        <footer class="footer"><ng-content select="[uiDialogFooter]" /></footer>
      </div>
    </dialog>
  `,
  styleUrl: './overlay.css',
})
export class UiModal extends UiDialogBase {
  readonly heading = input.required<string>();
  readonly size = input<'md' | 'lg'>('md');
  protected readonly messages = injectUiMessages();
}
