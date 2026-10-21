import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { UiButton } from '../button/button';
import { UiIcon } from '../icon/icon';
import { IconName } from '../icon/icons';
import { injectUiMessages } from '../core/ui-messages';

/** Nothing to show (empty cart, no search results). Put the next action in the content slot. */
@Component({
  selector: 'ui-empty-state',
  imports: [UiIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ui-icon class="art" [name]="icon()" />
    <h2 class="title">{{ heading() ?? messages.emptyTitle }}</h2>
    @if (description()) {
      <p class="description">{{ description() }}</p>
    }
    <div class="actions"><ng-content /></div>
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--space-3);
      padding: var(--space-12) var(--space-4);
      text-align: center;
    }
    .art {
      --icon-size: 3rem;
      color: var(--color-text-subtle);
    }
    .title {
      font-size: var(--font-size-xl);
    }
    .description {
      max-inline-size: 40ch;
      margin: 0;
      color: var(--color-text-muted);
    }
    .actions:not(:empty) {
      margin-block-start: var(--space-2);
    }
  `,
})
export class UiEmptyState {
  readonly icon = input<IconName>('package');
  readonly heading = input<string>();
  readonly description = input<string>();
  protected readonly messages = injectUiMessages();
}

/** Failed load/submit. Offers retry and an optional support reference (e.g. the API's traceId). */
@Component({
  selector: 'ui-error-state',
  imports: [UiIcon, UiButton],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { role: 'alert' },
  template: `
    <ui-icon class="art" name="alert" />
    <h2 class="title">{{ heading() ?? messages.errorTitle }}</h2>
    <p class="description">{{ description() ?? messages.errorMessage }}</p>
    <button uiButton variant="secondary" type="button" (click)="retry.emit()">{{ messages.retry }}</button>
    @if (reference()) {
      <p class="reference">{{ messages.errorReference }}: <bdi dir="ltr">{{ reference() }}</bdi></p>
    }
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: var(--space-3);
      padding: var(--space-12) var(--space-4);
      text-align: center;
    }
    .art {
      --icon-size: 3rem;
      color: var(--color-danger);
    }
    .title {
      font-size: var(--font-size-xl);
    }
    .description {
      max-inline-size: 40ch;
      margin: 0;
      color: var(--color-text-muted);
    }
    .reference {
      margin: 0;
      font-size: var(--font-size-xs);
      color: var(--color-text-subtle);
    }
  `,
})
export class UiErrorState {
  readonly heading = input<string>();
  readonly description = input<string>();
  readonly reference = input<string | null | undefined>();
  readonly retry = output<void>();
  protected readonly messages = injectUiMessages();
}
