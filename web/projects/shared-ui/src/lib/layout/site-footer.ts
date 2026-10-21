import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { UiIcon } from '../icon/icon';
import { NavItem } from './site-header';
import { injectUiMessages } from '../core/ui-messages';

export interface FooterGroup {
  title: string;
  links: readonly NavItem[];
}

/**
 * Storefront footer: about text, link groups (policies, help, categories) and contact details.
 * Slot [uiFooterBadges] is for trust marks (e.g. e-Namad) supplied by the shop owner.
 */
@Component({
  selector: 'ui-site-footer',
  imports: [RouterLink, UiIcon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <footer class="footer">
      <div class="ui-container grid">
        <section class="about">
          <p class="brand">{{ brandName() }}</p>
          @if (about()) {
            <p class="text">{{ about() }}</p>
          }
          <address class="contact">
            @if (phone()) {
              <a href="tel:{{ phone() }}"><ui-icon name="phone" /> <bdi dir="ltr">{{ phone() }}</bdi></a>
            }
            @if (email()) {
              <a href="mailto:{{ email() }}"><ui-icon name="mail" /> <bdi dir="ltr">{{ email() }}</bdi></a>
            }
            @if (address()) {
              <span><ui-icon name="map-pin" /> {{ address() }}</span>
            }
          </address>
        </section>

        <nav class="groups" [attr.aria-label]="messages.footerNavigation">
          @for (group of groups(); track group.title) {
            <section>
              <h2 class="group-title">{{ group.title }}</h2>
              <ul>
                @for (link of group.links; track link.link) {
                  <li><a [routerLink]="link.link">{{ link.label }}</a></li>
                }
              </ul>
            </section>
          }
        </nav>

        <div class="badges"><ng-content select="[uiFooterBadges]" /></div>
      </div>
      <div class="legal">
        <div class="ui-container">
          <small>{{ copyright() }}</small>
        </div>
      </div>
    </footer>
  `,
  styles: `
    :host {
      display: block;
    }
    .footer {
      margin-block-start: var(--space-16);
      background: var(--color-surface-muted);
      border-block-start: 1px solid var(--color-border);
    }
    .grid {
      display: grid;
      gap: var(--space-8);
      padding-block: var(--space-10);
    }
    .brand {
      margin: 0 0 var(--space-2);
      font-size: var(--font-size-xl);
      font-weight: var(--font-weight-bold);
    }
    .text {
      max-inline-size: 45ch;
      color: var(--color-text-muted);
    }
    .contact {
      display: flex;
      flex-direction: column;
      gap: var(--space-2);
      font-style: normal;
      font-size: var(--font-size-sm);
    }
    .contact a,
    .contact span {
      display: inline-flex;
      align-items: center;
      gap: var(--space-2);
      min-block-size: var(--control-height-sm);
      color: var(--color-text);
      text-decoration: none;
    }
    .contact a:hover {
      color: var(--color-primary);
    }
    .groups {
      display: grid;
      grid-template-columns: repeat(2, minmax(0, 1fr));
      gap: var(--space-6);
    }
    .group-title {
      margin-block-end: var(--space-2);
      font-size: var(--font-size-md);
    }
    ul {
      margin: 0;
      padding: 0;
      list-style: none;
    }
    .groups a {
      display: inline-flex;
      align-items: center;
      min-block-size: var(--control-height-sm);
      color: var(--color-text-muted);
      text-decoration: none;
    }
    .groups a:hover {
      color: var(--color-primary);
      text-decoration: underline;
    }
    .badges:empty {
      display: none;
    }
    .legal {
      border-block-start: 1px solid var(--color-border);
      padding-block: var(--space-4);
      color: var(--color-text-muted);
    }
    @media (min-width: 768px) {
      .grid {
        grid-template-columns: minmax(0, 1.2fr) minmax(0, 2fr);
      }
      .groups {
        grid-template-columns: repeat(3, minmax(0, 1fr));
      }
      .badges {
        grid-column: 1 / -1;
      }
    }
  `,
})
export class UiSiteFooter {
  readonly brandName = input.required<string>();
  readonly about = input<string>();
  readonly groups = input<readonly FooterGroup[]>([]);
  readonly phone = input<string>();
  readonly email = input<string>();
  readonly address = input<string>();
  readonly copyright = input.required<string>();
  protected readonly messages = injectUiMessages();
}
