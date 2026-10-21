import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { UiIcon } from '../icon/icon';
import { injectUiMessages } from '../core/ui-messages';

export interface BreadcrumbItem {
  label: string;
  /** Router link; omit for the current page (last item). */
  link?: string | readonly string[];
}

/**
 * Visual breadcrumb trail. The structured data (BreadcrumbList JSON-LD) is produced separately by the
 * SEO layer from the same items, so both always agree.
 */
@Component({
  selector: 'ui-breadcrumb',
  imports: [RouterLink, UiIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav [attr.aria-label]="messages.breadcrumb">
      <ol>
        @for (item of items(); track $index; let last = $last) {
          <li>
            @if (!last && item.link) {
              <a [routerLink]="item.link">{{ item.label }}</a>
              <ui-icon name="chevron-start" class="sep" />
            } @else {
              <span [attr.aria-current]="last ? 'page' : null">{{ item.label }}</span>
            }
          </li>
        }
      </ol>
    </nav>
  `,
  styles: `
    ol {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--space-1) var(--space-2);
      margin: 0;
      padding: 0;
      list-style: none;
      font-size: var(--font-size-sm);
    }
    li {
      display: inline-flex;
      align-items: center;
      gap: var(--space-2);
      color: var(--color-text-muted);
    }
    a {
      color: var(--color-text-muted);
    }
    a:hover {
      color: var(--color-primary);
    }
    li:last-child span {
      color: var(--color-text);
      font-weight: var(--font-weight-medium);
    }
    .sep {
      --icon-size: 0.875rem;
      color: var(--color-text-subtle);
    }
  `,
})
export class UiBreadcrumb {
  readonly items = input.required<readonly BreadcrumbItem[]>();
  protected readonly messages = injectUiMessages();
}
