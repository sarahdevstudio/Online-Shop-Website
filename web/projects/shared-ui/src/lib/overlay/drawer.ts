import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { UiIcon } from '../icon/icon';
import { injectUiMessages } from '../core/ui-messages';
import { UiDialogBase } from './dialog-base';

/**
 * Side panel for the mobile menu, filters and the mini cart. `side` is logical: "end" is the left
 * edge in RTL and the right edge in LTR.
 */
@Component({
  selector: 'ui-drawer',
  imports: [UiIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dialog
      #dialog
      class="dialog drawer"
      [class.side-start]="side() === 'start'"
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
export class UiDrawer extends UiDialogBase {
  readonly heading = input.required<string>();
  readonly side = input<'start' | 'end'>('end');
  protected readonly messages = injectUiMessages();
}
